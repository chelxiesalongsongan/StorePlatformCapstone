using Microsoft.EntityFrameworkCore;
using PaymentService.Models;
using Store.Messaging;

namespace PaymentService.Data;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Payment>().HasIndex(p => p.OrderId).IsUnique();   // one payment per order => idempotent charge
        b.Entity<Payment>().Property(p => p.Status).HasConversion<string>();
        b.Entity<ProcessedMessage>().HasKey(p => new { p.MessageId, p.Consumer });
    }
}
