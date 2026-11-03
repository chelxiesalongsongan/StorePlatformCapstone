using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;
using Store.Contracts;
using Store.Messaging;

namespace PaymentService.Messaging;

/// Compensation: refund a charged order. Already refunded or never charged => nothing to do.
public class RefundRequestedConsumer(
    RabbitConnection conn, IServiceScopeFactory scopes, ILogger<RefundRequestedConsumer> log)
    : RabbitConsumer<RefundRequested>(conn, scopes, log)
{
    protected override string Queue => "payment.refund";
    protected override string[] BindingKeys => [RoutingKeys.RefundRequested];

    protected override async Task HandleAsync(RefundRequested message, string messageId, IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<PaymentDbContext>();

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == Queue, ct))
        {
            Log.LogInformation("Duplicate message {MessageId} for order {OrderId} skipped", messageId, message.OrderId);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == message.OrderId, ct);
        if (payment is { Status: PaymentStatus.Charged })
        {
            payment.Status = PaymentStatus.Refunded;
            Log.LogInformation("Payment {PaymentId} for order {OrderId} refunded", payment.Id, message.OrderId);
        }
        else
        {
            Log.LogInformation("Nothing to refund for order {OrderId} (status: {Status})",
                message.OrderId, payment?.Status.ToString() ?? "no payment");
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId, Consumer = Queue, ProcessedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
