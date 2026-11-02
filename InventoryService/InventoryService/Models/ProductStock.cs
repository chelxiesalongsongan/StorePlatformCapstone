namespace InventoryService.Models;

public class ProductStock
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int QuantityAvailable { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
