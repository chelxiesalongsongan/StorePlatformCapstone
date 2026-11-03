namespace Store.Contracts;

public record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderPlaced(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderLine> Lines, decimal Total);
public record StockReserved(Guid OrderId, decimal Total);
public record StockReservationFailed(Guid OrderId, string Reason);
public record PaymentCharged(Guid OrderId, Guid PaymentId, decimal Amount);
public record PaymentFailed(Guid OrderId, string Reason);
public record StockReleaseRequested(Guid OrderId);
public record RefundRequested(Guid OrderId, Guid PaymentId);

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
