namespace OrderService.Models;

public class OrderInput
{
    public string CustomerName { get; set; } = string.Empty;

    public List<OrderItemInput> Items { get; set; } = new();

    public bool ForcePaymentFailure { get; set; } = false;
}
