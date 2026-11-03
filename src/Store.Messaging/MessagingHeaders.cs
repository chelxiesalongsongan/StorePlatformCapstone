namespace Store.Messaging;

public static class MessagingHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
}

public class RabbitOptions
{
    public string Host { get; set; } = "localhost";
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
