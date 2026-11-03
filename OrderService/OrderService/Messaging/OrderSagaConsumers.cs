using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using Store.Contracts;
using Store.Messaging;

namespace OrderService.Messaging;

/// Shared saga step: move an order to a new status in one local transaction,
/// with a history row and the idempotency record.
internal static class OrderSaga
{
    public static async Task<Order?> MoveToAsync(
        OrderDbContext db, int orderId, string newStatus, string? reason,
        string messageId, string consumer, ILogger log, CancellationToken ct)
    {
        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == consumer, ct))
        {
            log.LogInformation("Duplicate message {MessageId} for order {OrderId} skipped", messageId, orderId);
            return null;
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new InvalidOperationException($"Order {orderId} does not exist");

        Order? changed = null;
        if (order.Status is OrderStatuses.Confirmed or OrderStatuses.Cancelled)
        {
            log.LogWarning("Order {OrderId} is already {Status}; {NewStatus} ignored", order.Id, order.Status, newStatus);
        }
        else
        {
            order.Status = newStatus;
            order.FailureReason = reason;
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id, Status = newStatus, CreatedAt = DateTime.UtcNow
            });
            changed = order;
            log.LogInformation("Order {OrderId} moved to {Status}", order.Id, newStatus);
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = consumer, ProcessedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return changed;
    }
}

public class OrderStockReservedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<OrderStockReservedConsumer> log)
    : RabbitConsumer<StockReserved>(conn, scopes, log)
{
    protected override string Queue => "order.stock-reserved";
    protected override string[] BindingKeys => [RoutingKeys.StockReserved];

    protected override Task HandleAsync(StockReserved message, string messageId, IServiceProvider services, CancellationToken ct) =>
        OrderSaga.MoveToAsync(services.GetRequiredService<OrderDbContext>(), message.OrderId,
            OrderStatuses.StockReserved, null, messageId, Queue, Log, ct);
}

public class OrderPaymentChargedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<OrderPaymentChargedConsumer> log)
    : RabbitConsumer<PaymentCharged>(conn, scopes, log)
{
    protected override string Queue => "order.payment-charged";
    protected override string[] BindingKeys => [RoutingKeys.PaymentCharged];

    protected override Task HandleAsync(PaymentCharged message, string messageId, IServiceProvider services, CancellationToken ct) =>
        OrderSaga.MoveToAsync(services.GetRequiredService<OrderDbContext>(), message.OrderId,
            OrderStatuses.Confirmed, null, messageId, Queue, Log, ct);
}

public class OrderStockReservationFailedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<OrderStockReservationFailedConsumer> log)
    : RabbitConsumer<StockReservationFailed>(conn, scopes, log)
{
    protected override string Queue => "order.stock-reservation-failed";
    protected override string[] BindingKeys => [RoutingKeys.StockReservationFailed];

    // Nothing was reserved (all-or-nothing), so cancelling is the whole compensation.
    protected override Task HandleAsync(StockReservationFailed message, string messageId, IServiceProvider services, CancellationToken ct) =>
        OrderSaga.MoveToAsync(services.GetRequiredService<OrderDbContext>(), message.OrderId,
            OrderStatuses.Cancelled, message.Reason, messageId, Queue, Log, ct);
}

public class OrderPaymentFailedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<OrderPaymentFailedConsumer> log)
    : RabbitConsumer<PaymentFailed>(conn, scopes, log)
{
    protected override string Queue => "order.payment-failed";
    protected override string[] BindingKeys => [RoutingKeys.PaymentFailed];

    protected override async Task HandleAsync(PaymentFailed message, string messageId, IServiceProvider services, CancellationToken ct)
    {
        var cancelled = await OrderSaga.MoveToAsync(services.GetRequiredService<OrderDbContext>(), message.OrderId,
            OrderStatuses.Cancelled, message.Reason, messageId, Queue, Log, ct);
        if (cancelled is null) return; // duplicate or already finished

        // Compensation: give the stock back and refund (refund is a no-op if nothing was charged).
        var publisher = services.GetRequiredService<IEventPublisher>();
        publisher.Publish(RoutingKeys.StockReleaseRequested, new StockReleaseRequested(message.OrderId));
        publisher.Publish(RoutingKeys.RefundRequested, new RefundRequested(message.OrderId));
        Log.LogInformation("Compensation requested for order {OrderId}: release stock and refund", message.OrderId);
    }
}
