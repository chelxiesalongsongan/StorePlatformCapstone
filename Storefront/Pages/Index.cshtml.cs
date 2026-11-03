using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class IndexModel(GatewayClient gateway, ILogger<IndexModel> log) : PageModel
{
    public IReadOnlyList<Product> Products { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Public route: works without logging in.
            Products = await gateway.GetAsync<List<Product>>("catalog/v1/products");
        }
        catch (Exception ex) when (ex is GatewayException or HttpRequestException)
        {
            log.LogWarning(ex, "Could not load products");
            ErrorMessage = "Products are unavailable right now. Please try again shortly.";
        }
    }
}
