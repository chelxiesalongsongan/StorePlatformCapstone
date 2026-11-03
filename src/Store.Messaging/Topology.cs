using RabbitMQ.Client;
using Store.Contracts;

namespace Store.Messaging;

public static class Topology
{
    /// Declares the main exchange, the dead-letter exchange, the queue, and its .dlq.
    public static void DeclareQueue(IModel ch, string queue, params string[] bindingKeys)
    {
        ch.ExchangeDeclare(RoutingKeys.Exchange, ExchangeType.Topic, durable: true);
        ch.ExchangeDeclare(RoutingKeys.DeadLetterExchange, ExchangeType.Direct, durable: true);

        var dlq = $"{queue}.dlq";
        ch.QueueDeclare(dlq, durable: true, exclusive: false, autoDelete: false);
        ch.QueueBind(dlq, RoutingKeys.DeadLetterExchange, routingKey: queue);

        ch.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = RoutingKeys.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            });

        foreach (var key in bindingKeys)
            ch.QueueBind(queue, RoutingKeys.Exchange, key);
    }
}
