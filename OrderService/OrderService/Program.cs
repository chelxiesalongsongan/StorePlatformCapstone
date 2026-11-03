using Microsoft.EntityFrameworkCore;
using OrderService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseSqlite(
        
builder.Configuration.GetConnectionString("OrderDb")));

// Correlation ID handler
builder.Services.AddTransient<OnlineStore.Shared.CorrelationIdHandler>();

// Catalog Service typed HTTP client
builder.Services.AddHttpClient<OrderService.Clients.CatalogApiClient>()
    .AddHttpMessageHandler<OnlineStore.Shared.CorrelationIdHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddTransient<OrderService.Clients.CatalogApiClient>(serviceProvider =>
{
    var httpClient = serviceProvider
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(OrderService.Clients.CatalogApiClient));

    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    return new OrderService.Clients.CatalogApiClient(httpClient)
    {
        BaseUrl = configuration["Services:Catalog"]!
    };
});


// Inventory Service typed HTTP client
builder.Services.AddHttpClient<InventoryService.Clients.InventoryApiClient>()
    .AddHttpMessageHandler<OnlineStore.Shared.CorrelationIdHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddTransient<InventoryService.Clients.InventoryApiClient>(serviceProvider =>
{
    var httpClient = serviceProvider
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(InventoryService.Clients.InventoryApiClient));

    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    return new InventoryService.Clients.InventoryApiClient(httpClient)
    {
        BaseUrl = configuration["Services:Inventory"]!
    };
});


// Payment Service typed HTTP client
builder.Services.AddHttpClient<PaymentService.Clients.PaymentApiClient>()
    .AddHttpMessageHandler<OnlineStore.Shared.CorrelationIdHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddTransient<PaymentService.Clients.PaymentApiClient>(serviceProvider =>
{
    var httpClient = serviceProvider
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(PaymentService.Clients.PaymentApiClient));

    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    return new PaymentService.Clients.PaymentApiClient(httpClient)
    {
        BaseUrl = configuration["Services:Payment"]!
    };
});

// RabbitMQ and the order saga (Module 4)
Store.Messaging.MessagingServiceCollectionExtensions.AddRabbitMessaging(builder.Services, builder.Configuration);
builder.Services.AddHostedService<OrderService.Messaging.OrderStockReservedConsumer>();
builder.Services.AddHostedService<OrderService.Messaging.OrderPaymentChargedConsumer>();
builder.Services.AddHostedService<OrderService.Messaging.OrderStockReservationFailedConsumer>();
builder.Services.AddHostedService<OrderService.Messaging.OrderPaymentFailedConsumer>();

var app = builder.Build();
app.UseMiddleware<OnlineStore.Shared.CorrelationIdMiddleware>();

// Apply migrations at startup so a clean clone has the order tables
using (var migrationScope = app.Services.CreateScope())
{
    migrationScope.ServiceProvider.GetRequiredService<OrderService.Data.OrderDbContext>().Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider
        .GetRequiredService<OrderDbContext>()
        .Database.Migrate();
}

app.UseMiddleware<OnlineStore.Shared.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();


