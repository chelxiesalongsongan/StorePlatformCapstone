namespace OrderService.Models;

public class OrderItemInput
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public double UnitPrice { get; set; }
}