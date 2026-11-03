using System.ComponentModel.DataAnnotations;

namespace AuthService.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record UserResponse(int Id, string Email, string Role);

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string Email, string Role);
