using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Local runs use localhost ports; Docker uses service names.
var ocelotFile = builder.Environment.IsDevelopment() ? "ocelot.Development.json" : "ocelot.json";
builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);

// JWT settings must match the Auth service (env vars Jwt__Key, Jwt__Issuer, Jwt__Audience).
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set (env var Jwt__Key) and be at least 32 characters.");

builder.Services
    .AddAuthentication()
    .AddJwtBearer("Bearer", o =>
    {
        o.MapInboundClaims = false; // keep "role" and "sub" as-is so Ocelot's claim checks match
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// The correlation ID is created here, at the edge, and forwarded to every service.
app.Use(async (context, next) =>
{
    const string header = "X-Correlation-ID";
    if (!context.Request.Headers.TryGetValue(header, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
        context.Request.Headers[header] = correlationId;
    }
    context.Response.Headers[header] = correlationId;

    // Identify the caller for rate limiting (per client IP) when no ClientId header is sent.
    if (!context.Request.Headers.ContainsKey("ClientId"))
        context.Request.Headers["ClientId"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId.ToString() }))
    {
        app.Logger.LogInformation("Gateway {Method} {Path}", context.Request.Method, context.Request.Path);
        await next();
    }
});

await app.UseOcelot();
app.Run();

