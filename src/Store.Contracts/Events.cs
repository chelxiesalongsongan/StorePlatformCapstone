namespace Store.Contracts;

public record OrderLine(int ProductId, int Quantity, decimal UnitPrice);

public record OrderPlaced(int OrderId, string CustomerId, IReadOnlyList<OrderLine> Lines, decimal Total);
public record StockReserved(int OrderId, decimal Total);
public record StockReservationFailed(int OrderId, string Reason);
public record PaymentCharged(int OrderId, decimal Amount);
public record PaymentFailed(int OrderId, string Reason);
public record StockReleaseRequested(int OrderId);
public record RefundRequested(int OrderId);

public static class RoutingKeys
{
    public const string Exchange = "store.events";
    public const string DeadLetterExchange = "store.dlx";

    public const string OrderPlaced = "order.placed";
    public const string StockReserved = "stock.reserved";
    public const string StockReservationFailed = "stock.reservation-failed";
    public const string PaymentCharged = "payment.charged";
    public const string PaymentFailed = "payment.failed";
    public const string StockReleaseRequested = "stock.release-requested";
    public const string RefundRequested = "payment.refund-requested";
}
