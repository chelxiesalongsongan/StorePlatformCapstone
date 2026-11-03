using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Store.Messaging;

public sealed class RabbitConnection : IDisposable
{
    public IConnection Connection { get; }

    public RabbitConnection(IOptions<RabbitOptions> options, ILogger<RabbitConnection> log)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host,
            UserName = o.User,
            Password = o.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        // In Docker the broker may start after this service, so retry.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Connection = factory.CreateConnection();
                log.LogInformation("Connected to RabbitMQ at {Host}", o.Host);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                log.LogWarning(ex, "RabbitMQ not ready (attempt {Attempt}/10), retrying in 3s", attempt);
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }
    }

    public void Dispose() => Connection.Dispose();
}
