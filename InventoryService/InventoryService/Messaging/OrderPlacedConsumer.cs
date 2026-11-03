using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;
using Store.Contracts;
using Store.Messaging;

namespace InventoryService.Messaging;

/// Saga step 1: reserve stock for every line of an order, all or nothing.
public class OrderPlacedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<OrderPlacedConsumer> log)
    : RabbitConsumer<OrderPlaced>(conn, scopes, log)
{
    protected override string Queue => "inventory.order-placed";
    protected override string[] BindingKeys => [RoutingKeys.OrderPlaced];

    protected override async Task HandleAsync(OrderPlaced message, string messageId, IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<InventoryDbContext>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == Queue, ct))
        {
            Log.LogInformation("Duplicate message {MessageId} for order {OrderId} skipped", messageId, message.OrderId);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var wanted = message.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var productIds = wanted.Keys.ToList();
        var stocks = await db.ProductStocks
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, ct);

        string? failure = null;
        foreach (var (productId, quantity) in wanted)
        {
            if (!stocks.TryGetValue(productId, out var stock))
            { failure = $"No inventory record for product {productId}"; break; }
            if (stock.QuantityAvailable < quantity)
            { failure = $"Insufficient stock for product {productId}"; break; }
        }

        if (failure is null)
        {
            foreach (var (productId, quantity) in wanted)
            {
                var stock = stocks[productId];
                stock.QuantityAvailable -= quantity;
                stock.UpdatedAt = DateTime.UtcNow;

                db.Reservations.Add(new Reservation
                {
                    ProductId = productId,
                    OrderId = message.OrderId,
                    Quantity = quantity,
                    Status = ReservationStatus.Reserved
                });
            }
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = Queue, ProcessedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (failure is null)
        {
            Log.LogInformation("Stock reserved for order {OrderId}", message.OrderId);
            publisher.Publish(RoutingKeys.StockReserved, new StockReserved(message.OrderId, message.Total));
        }
        else
        {
            Log.LogWarning("Reservation failed for order {OrderId}: {Reason}", message.OrderId, failure);
            publisher.Publish(RoutingKeys.StockReservationFailed, new StockReservationFailed(message.OrderId, failure));
        }
    }
}
