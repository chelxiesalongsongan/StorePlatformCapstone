namespace InventoryService.Models;

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int ProductId { get; set; }

    public int OrderId { get; set; }

    public int Quantity { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReleasedAt { get; set; }
}

public enum ReservationStatus
{
    Reserved,
    Released
}
