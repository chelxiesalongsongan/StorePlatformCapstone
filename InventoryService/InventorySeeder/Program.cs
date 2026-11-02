using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

var databasePath = Path.GetFullPath(
    Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "InventoryService",
        "InventoryDB.db"));

var options = new DbContextOptionsBuilder<InventoryDbContext>()
    .UseSqlite($"Data Source={databasePath}")
    .Options;

await using var context = new InventoryDbContext(options);

var samples = new[]
{
    new ProductStock { ProductId = 1, QuantityAvailable = 10 },
    new ProductStock { ProductId = 2, QuantityAvailable = 20 },
    new ProductStock { ProductId = 3, QuantityAvailable = 15 }
};

foreach (var sample in samples)
{
    var existing = await context.ProductStocks
        .FirstOrDefaultAsync(p => p.ProductId == sample.ProductId);

    if (existing == null)
    {
        context.ProductStocks.Add(sample);
        Console.WriteLine($"Added Product {sample.ProductId}: {sample.QuantityAvailable} units");
    }
    else
    {
        Console.WriteLine($"Product {sample.ProductId} already exists. Skipped.");
    }
}

await context.SaveChangesAsync();

Console.WriteLine("Inventory seeding completed!");
