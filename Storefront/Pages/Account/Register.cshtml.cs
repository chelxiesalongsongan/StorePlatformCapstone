using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Services;

namespace Storefront.Pages.Account;

public class RegisterModel(GatewayClient gateway) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            await gateway.PostAsync("auth/v1/register", new { email = Input.Email, password = Input.Password });
            TempData["Message"] = "Account created. Please log in.";
            return RedirectToPage("/Account/Login");
        }
        catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists.");
        }
        catch (GatewayException ex)
        {
            ModelState.AddModelError(string.Empty, $"Registration failed: {ex.Message}");
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The store is unavailable right now. Please try again shortly.");
        }
        return Page();
    }
}
