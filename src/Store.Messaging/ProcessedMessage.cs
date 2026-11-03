namespace Store.Messaging;

/// Idempotency guard row: one per (MessageId, Consumer) already processed.
public class ProcessedMessage
{
    public string MessageId { get; set; } = default!;
    public string Consumer { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}
