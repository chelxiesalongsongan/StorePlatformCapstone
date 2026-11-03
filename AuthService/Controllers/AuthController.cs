using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("auth/v1")]
public class AuthController(AuthDbContext db, TokenService tokens, ILogger<AuthController> log) : ControllerBase
{
    private static readonly PasswordHasher<User> Hasher = new();

    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Problem(statusCode: 409, title: "Email already registered",
                detail: "An account with this email already exists.");

        var user = new User { Email = email, Role = Roles.Customer };
        user.PasswordHash = Hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        log.LogInformation("Registered user {UserId}", user.Id);
        return StatusCode(StatusCodes.Status201Created, new UserResponse(user.Id, user.Email, user.Role));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null ||
            Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            log.LogWarning("Failed login attempt");
            return Problem(statusCode: 401, title: "Invalid credentials",
                detail: "Email or password is incorrect.");
        }

        return Ok(tokens.CreateToken(user));
    }
}
