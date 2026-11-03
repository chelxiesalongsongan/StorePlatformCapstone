namespace Storefront.Models;

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string Email, string Role);

public record Product(int Id, string Name, string? Description, decimal Price, int StockQuantity);
