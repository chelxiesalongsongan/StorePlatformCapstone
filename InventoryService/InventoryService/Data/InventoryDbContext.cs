using InventoryService.Models;
using Microsoft.EntityFrameworkCore;
using Store.Messaging;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductStock>()
            .HasIndex(p => p.ProductId)
            .IsUnique();

        modelBuilder.Entity<Reservation>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<ProcessedMessage>()
            .HasKey(p => new { p.MessageId, p.Consumer });
    }
}
