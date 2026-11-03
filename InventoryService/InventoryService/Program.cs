using InventoryService.Data;
using InventoryService.Messaging;
using Microsoft.EntityFrameworkCore;
using Store.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite Database
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("InventoryDatabase")));

// Correlation ID Middleware
builder.Services.AddTransient<OnlineStore.Shared.CorrelationIdHandler>();

// RabbitMQ (Module 4)
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddHostedService<OrderPlacedConsumer>();
builder.Services.AddHostedService<StockReleaseRequestedConsumer>();

var app = builder.Build();

// Apply migrations at startup so a clean clone has its tables
using (var scope = app.Services.CreateScope())
{
    var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    inventoryDb.Database.Migrate();

    // Starting stock for the catalog's products, so a clean clone can take orders.
    var seedStock = new Dictionary<int, int> { [1] = 25, [2] = 15 };
    foreach (var (productId, quantity) in seedStock)
    {
        if (!inventoryDb.ProductStocks.Any(s => s.ProductId == productId))
            inventoryDb.ProductStocks.Add(new InventoryService.Models.ProductStock { ProductId = productId, QuantityAvailable = quantity });
    }
    inventoryDb.SaveChanges();
}

app.UseMiddleware<OnlineStore.Shared.CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

