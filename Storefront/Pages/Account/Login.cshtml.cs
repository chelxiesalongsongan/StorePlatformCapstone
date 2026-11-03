using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages.Account;

public class LoginModel(GatewayClient gateway) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var auth = await gateway.PostAsync<AuthResponse>("auth/v1/login",
                new { email = Input.Email, password = Input.Password });

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, auth.Email),
                new(ClaimTypes.Role, auth.Role),
                new(AuthClaims.AccessToken, auth.AccessToken)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { ExpiresUtc = auth.ExpiresAtUtc });

            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }
        catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }
        catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ModelState.AddModelError(string.Empty, "Too many login attempts. Please wait a minute and try again.");
        }
        catch (GatewayException ex)
        {
            ModelState.AddModelError(string.Empty, $"Login failed: {ex.Message}");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The store is unavailable right now. Please try again shortly.");
        }
        return Page();
    }
}
