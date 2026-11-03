using Microsoft.AspNetCore.Authentication.Cookies;
using Storefront.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages()
    .AddMvcOptions(o => o.Filters.Add<GatewayErrorPageFilter>());

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.Cookie.Name = "storefront.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(2);
        o.SlidingExpiration = false;
    });

// The Storefront talks to the gateway only (env var Gateway__BaseUrl).
var gatewayUrl = builder.Configuration["Gateway:BaseUrl"]
    ?? throw new InvalidOperationException("Gateway:BaseUrl must be set (env var Gateway__BaseUrl).");
if (!gatewayUrl.EndsWith('/')) gatewayUrl += "/";

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<GatewayClient>(c => c.BaseAddress = new Uri(gatewayUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
