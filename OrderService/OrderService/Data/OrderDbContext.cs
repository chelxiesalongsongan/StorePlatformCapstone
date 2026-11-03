using Microsoft.EntityFrameworkCore;
using OrderService.Models;
using Store.Messaging;

namespace OrderService.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderStatusHistory> OrderStatusHistories
        => Set<OrderStatusHistory>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<OrderStatusHistory>().HasIndex(h => h.OrderId);

        b.Entity<ProcessedMessage>().HasKey(p => new { p.MessageId, p.Consumer });
    }
}
