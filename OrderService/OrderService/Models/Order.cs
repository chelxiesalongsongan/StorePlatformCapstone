namespace OrderService.Models;

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string StockReserved = "StockReserved";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = OrderStatuses.Pending;
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
