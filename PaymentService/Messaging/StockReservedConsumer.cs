using Microsoft.EntityFrameworkCore;
using OnlineStore.Shared;
using PaymentService.Data;
using PaymentService.Models;
using PaymentService.Simulation;
using Store.Contracts;
using Store.Messaging;

namespace PaymentService.Messaging;

/// Saga step 2: stock is reserved, so charge the order (or decline it when the switch is on).
public class StockReservedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<StockReservedConsumer> log)
    : RabbitConsumer<StockReserved>(conn, scopes, log)
{
    protected override string Queue => "payment.stock-reserved";
    protected override string[] BindingKeys => [RoutingKeys.StockReserved];

    protected override async Task HandleAsync(StockReserved message, string messageId, IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<PaymentDbContext>();
        var publisher = services.GetRequiredService<IEventPublisher>();
        var failureSwitch = services.GetRequiredService<PaymentFailureSwitch>();

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == Queue, ct))
        {
            Log.LogInformation("Duplicate message {MessageId} for order {OrderId} skipped", messageId, message.OrderId);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // One payment per order: if a payment already exists, reuse its outcome.
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId, ct);
        if (payment is null)
        {
            payment = new Payment
            {
                OrderId = message.OrderId,
                Amount = message.Total,
                Status = failureSwitch.FailPayments ? PaymentStatus.Failed : PaymentStatus.Charged,
                CorrelationId = Correlation.Current
            };
            db.Payments.Add(payment);
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = Queue, ProcessedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (payment.Status == PaymentStatus.Failed)
        {
            Log.LogWarning("Payment declined for order {OrderId}", message.OrderId);
            publisher.Publish(RoutingKeys.PaymentFailed, new PaymentFailed(message.OrderId, "Payment declined"));
        }
        else
        {
            Log.LogInformation("Payment {PaymentId} charged {Amount} for order {OrderId}", payment.Id, payment.Amount, message.OrderId);
            publisher.Publish(RoutingKeys.PaymentCharged, new PaymentCharged(message.OrderId, payment.Amount));
        }
    }
}
