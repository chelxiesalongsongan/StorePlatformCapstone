using System.Text.Json;
using Microsoft.Extensions.Logging;
using OnlineStore.Shared;
using RabbitMQ.Client;
using Store.Contracts;

namespace Store.Messaging;

public interface IEventPublisher
{
    void Publish<T>(string routingKey, T message) where T : class;
}

public sealed class RabbitPublisher : IEventPublisher, IDisposable
{
    private readonly IModel _ch;
    private readonly ILogger<RabbitPublisher> _log;
    private readonly object _lock = new();

    public RabbitPublisher(RabbitConnection conn, ILogger<RabbitPublisher> log)
    {
        _log = log;
        _ch = conn.Connection.CreateModel();
        _ch.ExchangeDeclare(RoutingKeys.Exchange, ExchangeType.Topic, durable: true);
        _ch.ConfirmSelect(); // publisher confirms
        _ch.BasicReturn += (_, e) =>
            _log.LogWarning("Unroutable message returned: {RoutingKey} ({Reason})", e.RoutingKey, e.ReplyText);
    }

    public void Publish<T>(string routingKey, T message) where T : class
    {
        var props = _ch.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = Guid.NewGuid().ToString(); // used by idempotent consumers
        props.Type = typeof(T).Name;
        props.ContentType = "application/json";
        props.Headers = new Dictionary<string, object>
        {
            [MessagingHeaders.CorrelationId] = Correlation.Current
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        lock (_lock) // channels are not thread-safe
        {
            _ch.BasicPublish(RoutingKeys.Exchange, routingKey, mandatory: true, props, body);
            _ch.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5)); // throws if the broker does not confirm
        }

        _log.LogInformation("Published {Type} {MessageId} to {RoutingKey}", props.Type, props.MessageId, routingKey);
    }

    public void Dispose() => _ch.Dispose();
}
