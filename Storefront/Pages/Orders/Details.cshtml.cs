using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages.Orders;

[Authorize]
public class DetailsModel(GatewayClient gateway) : PageModel
{
    public OrderDetails Order { get; private set; } = default!;
    public Dictionary<int, string> ProductNames { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Order = await gateway.GetAsync<OrderDetails>($"orders/v1/orders/{id}");
        }
        catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        try
        {
            var products = await gateway.GetAsync<List<Product>>("catalog/v1/products");
            ProductNames = products.ToDictionary(p => p.Id, p => p.Name);
        }
        catch (Exception ex) when (ex is GatewayException or HttpRequestException)
        {
            // Product names are optional; the page falls back to product IDs.
        }
        return Page();
    }
}
