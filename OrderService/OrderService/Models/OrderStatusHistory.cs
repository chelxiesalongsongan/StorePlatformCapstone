namespace OrderService.Models;

public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}