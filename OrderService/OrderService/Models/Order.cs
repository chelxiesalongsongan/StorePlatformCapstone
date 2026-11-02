namespace OrderService.Models;

public class Order
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public List<OrderItem> Items { get; set; } = new();

    public double TotalAmount { get; set; }

    public List<Guid> ReservationIds { get; set; } = new();

    public int? PaymentId { get; set; }

    public string? PaymentStatus { get; set; }
}
