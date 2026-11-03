namespace Storefront.Models;

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
    IReadOnlyList<OrderStatusEntry> History)
{
    public bool IsFinished => Status is "Confirmed" or "Cancelled";

    public static string Label(string status) => status == "StockReserved" ? "Stock reserved" : status;
}

public record SimulationSettings(bool FailPayments);

public record StockLevel(int ProductId, int QuantityAvailable);
