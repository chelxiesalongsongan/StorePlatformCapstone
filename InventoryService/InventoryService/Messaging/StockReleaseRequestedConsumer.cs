using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;
using Store.Contracts;
using Store.Messaging;

namespace InventoryService.Messaging;

/// Compensation: release every active reservation for an order.
public class StockReleaseRequestedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<StockReleaseRequestedConsumer> log)
    : RabbitConsumer<StockReleaseRequested>(conn, scopes, log)
{
    protected override string Queue => "inventory.stock-release";
    protected override string[] BindingKeys => [RoutingKeys.StockReleaseRequested];

    protected override async Task HandleAsync(StockReleaseRequested message, string messageId, IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<InventoryDbContext>();

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == Queue, ct))
        {
            Log.LogInformation("Duplicate message {MessageId} for order {OrderId} skipped", messageId, message.OrderId);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var reservations = await db.Reservations
            .Where(r => r.OrderId == message.OrderId && r.Status == ReservationStatus.Reserved)
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            var stock = await db.ProductStocks.FirstOrDefaultAsync(s => s.ProductId == reservation.ProductId, ct);
            if (stock is not null)
            {
                stock.QuantityAvailable += reservation.Quantity;
                stock.UpdatedAt = DateTime.UtcNow;
            }
            reservation.Status = ReservationStatus.Released;
            reservation.ReleasedAt = DateTime.UtcNow;
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = Queue, ProcessedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        Log.LogInformation("Released {Count} reservation(s) for order {OrderId}", reservations.Count, message.OrderId);
    }
}
