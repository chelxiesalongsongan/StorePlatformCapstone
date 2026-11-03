namespace OrderService.Models;

public record OrderItemDetails(int ProductId, int Quantity, decimal UnitPrice);

public record OrderStatusEntry(string Status, DateTime At);

public record OrderDetails(
    int Id,
    string CustomerId,
    string CustomerName,
    string Status,
    string? FailureReason,
    decimal TotalAmount,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDetails> Items,
    IReadOnlyList<OrderStatusEntry> History);
