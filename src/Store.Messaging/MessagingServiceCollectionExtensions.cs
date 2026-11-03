using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Store.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// Reads the "Rabbit" section (env vars: Rabbit__Host, Rabbit__User, Rabbit__Password).
    public static IServiceCollection AddRabbitMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitOptions>(config.GetSection("Rabbit"));
        services.AddSingleton<RabbitConnection>();
        services.AddSingleton<IEventPublisher, RabbitPublisher>();
        return services;
    }
}
