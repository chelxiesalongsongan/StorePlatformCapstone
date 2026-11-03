using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages.Admin;

// Only login is required here. The Admin role is enforced by the API gateway,
// so a customer who submits a change gets a 403 from the gateway and sees "Admins only".
[Authorize]
public class StockModel(GatewayClient gateway) : PageModel
{
    public record StockRow(Product Product, int? Available);

    public IReadOnlyList<StockRow> Rows { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var products = await gateway.GetAsync<List<Product>>("catalog/v1/products");
            var rows = new List<StockRow>();
            foreach (var product in products)
            {
                int? available = null;
                try
                {
                    available = (await gateway.GetAsync<StockLevel>($"inventory/v1/products/{product.Id}/stock")).QuantityAvailable;
                }
                catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // No stock record yet for this product.
                }
                rows.Add(new StockRow(product, available));
            }
            Rows = rows;
        }
        catch (Exception ex) when (ex is GatewayException { StatusCode: not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) } or HttpRequestException)
        {
            ErrorMessage = "Stock levels couldn't load because the catalog or inventory service isn't responding.";
        }
    }

    public async Task<IActionResult> OnPostAdjustAsync(int productId, int quantityChange)
    {
        if (quantityChange == 0)
        {
            FlashError = "Enter a number other than zero. Use a negative number to remove stock.";
            return RedirectToPage();
        }

        try
        {
            await gateway.PutAsync($"inventory/v1/products/{productId}/stock", new { quantityChange });
            Flash = quantityChange > 0
                ? $"Added {quantityChange} to product {productId}."
                : $"Removed {-quantityChange} from product {productId}.";
        }
        catch (GatewayException ex) when (ex.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            FlashError = $"Stock wasn't changed: {ex.Message}";
        }
        catch (HttpRequestException)
        {
            FlashError = "Stock wasn't changed because the inventory service isn't responding.";
        }
        return RedirectToPage();
    }
}
