namespace CatalogService.Model
{
    public class ProductInput
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public double Price { get; set; }

        public int StockQuantity { get; set; }
    }
}