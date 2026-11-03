using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// JWT settings come from configuration / environment variables, never from code.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set (env var Jwt__Key) and be at least 32 characters.");

builder.Services.AddDbContext<AuthDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("AuthDb")));
builder.Services.AddScoped<TokenService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Migrate and seed the admin account on startup (credentials from configuration).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();

    var adminEmail = app.Configuration["Seed:AdminEmail"];
    var adminPassword = app.Configuration["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning("Seed:AdminEmail / Seed:AdminPassword not set; no admin account seeded.");
    }
    else if (!db.Users.Any(u => u.Role == Roles.Admin))
    {
        var admin = new User { Email = adminEmail.Trim().ToLowerInvariant(), Role = Roles.Admin };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        db.SaveChanges();
        app.Logger.LogInformation("Seeded admin account {Email}", admin.Email);
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
