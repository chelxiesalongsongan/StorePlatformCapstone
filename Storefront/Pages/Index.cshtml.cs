using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages;

public class IndexModel(GatewayClient gateway, ILogger<IndexModel> log) : PageModel
{
    public IReadOnlyList<Product> Products { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public SimulationSettings? Simulation { get; private set; }

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Public route: works without logging in.
            var products = await gateway.GetAsync<List<Product>>("catalog/v1/products");
            Products = await WithLiveStockAsync(products);
        }
        catch (Exception ex) when (ex is GatewayException or HttpRequestException)
        {
            log.LogWarning(ex, "Could not load products");
            ErrorMessage = "Products are unavailable right now.";
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            try
            {
                Simulation = await gateway.GetAsync<SimulationSettings>("payments/v1/simulation");
            }
            catch (Exception ex) when (ex is GatewayException { StatusCode: not HttpStatusCode.Unauthorized } or HttpRequestException)
            {
                log.LogWarning(ex, "Could not read the payment simulation switch");
            }
        }
    }

    // Inventory is the source of truth for stock; Catalog's stock figure isn't updated by orders.
    private async Task<List<Product>> WithLiveStockAsync(List<Product> products)
    {
        var lookups = products.Select(async product =>
        {
            try
            {
                var stock = await gateway.GetAsync<StockLevel>($"inventory/v1/products/{product.Id}/stock");
                return product with { StockQuantity = stock.QuantityAvailable };
            }
            catch (GatewayException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return product with { StockQuantity = 0 }; // no stock record, so it can't be ordered
            }
            catch (Exception ex) when (ex is GatewayException or HttpRequestException)
            {
                log.LogWarning(ex, "Live stock unavailable for product {ProductId}; showing the catalog figure", product.Id);
                return product;
            }
        });
        return (await Task.WhenAll(lookups)).ToList();
    }

    public async Task<IActionResult> OnPostOrderAsync(int productId, int quantity)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToPage("/Account/Login", new { returnUrl = "/" });
        if (quantity < 1) quantity = 1;

        try
        {
            var order = await gateway.PostAsync<OrderDetails>("orders/v1/orders", new
            {
                customerName = User.Identity!.Name,
                items = new[] { new { productId, quantity } }
            });
            return RedirectToPage("/Orders/Details", new { id = order.Id });
        }
        catch (GatewayException ex) when (ex.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            FlashError = $"The order wasn't placed: {ex.Message}";
        }
        catch (HttpRequestException)
        {
            FlashError = "The order wasn't placed because the store isn't responding. Try again in a moment.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSimulationAsync(bool failPayments)
    {
        try
        {
            await gateway.PutAsync("payments/v1/simulation", new { failPayments });
            Flash = failPayments
                ? "Payment failure simulation is on. The next order will be declined and compensated."
                : "Payment failure simulation is off. Orders are charged normally.";
        }
        catch (GatewayException ex) when (ex.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            FlashError = $"The simulation switch didn't change: {ex.Message}";
        }
        catch (HttpRequestException)
        {
            FlashError = "The payment service isn't responding, so the simulation switch didn't change.";
        }
        return RedirectToPage();
    }
}

