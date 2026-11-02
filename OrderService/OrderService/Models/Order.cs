namespace OrderService.Models;

public class Order
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public List<OrderItem> Items { get; set; } = new();

    public double TotalAmount { get; set; }
}