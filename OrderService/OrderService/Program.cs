var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

var app = builder.Build();

app.UseMiddleware<OnlineStore.Shared.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
