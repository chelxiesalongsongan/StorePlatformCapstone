using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlineStore.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Store.Messaging;

/// Base consumer: manual ack, correlation ID restored from headers,
/// failures nacked without requeue so they go to the .dlq (never dropped).
public abstract class RabbitConsumer<T> : BackgroundService where T : class
{
    private readonly RabbitConnection _conn;
    protected readonly IServiceScopeFactory Scopes;
    protected readonly ILogger Log;
    private IModel? _ch;

    protected abstract string Queue { get; }
    protected abstract string[] BindingKeys { get; }

    /// messageId is the broker MessageId, use it for the idempotency guard.
    protected abstract Task HandleAsync(T message, string messageId, IServiceProvider services, CancellationToken ct);

    protected RabbitConsumer(RabbitConnection conn, IServiceScopeFactory scopes, ILogger log)
    {
        _conn = conn;
        Scopes = scopes;
        Log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield(); // don't block app startup

        _ch = _conn.Connection.CreateModel();
        Topology.DeclareQueue(_ch, Queue, BindingKeys);
        _ch.BasicQos(0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += async (_, ea) =>
        {
            var correlationId = ReadHeader(ea.BasicProperties, MessagingHeaders.CorrelationId) ?? Guid.NewGuid().ToString();
            Correlation.Set(correlationId);

            using var scope = Log.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["MessageId"] = ea.BasicProperties.MessageId ?? "none"
            });

            try
            {
                var message = JsonSerializer.Deserialize<T>(ea.Body.Span)
                              ?? throw new InvalidOperationException("Empty message body");

                using var serviceScope = Scopes.CreateScope();
                await HandleAsync(message, ea.BasicProperties.MessageId ?? string.Empty, serviceScope.ServiceProvider, ct);

                _ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Processing failed on {Queue}; dead-lettering", Queue);
                _ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: false); // goes to {Queue}.dlq
            }
        };

        _ch.BasicConsume(Queue, autoAck: false, consumer);
        Log.LogInformation("Consuming {Queue}", Queue);
    }

    private static string? ReadHeader(IBasicProperties props, string name) =>
        props.Headers != null && props.Headers.TryGetValue(name, out var raw) && raw is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;

    public override void Dispose()
    {
        _ch?.Dispose();
        base.Dispose();
    }
}
