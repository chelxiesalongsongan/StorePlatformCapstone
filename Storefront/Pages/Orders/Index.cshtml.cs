using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Models;
using Storefront.Services;

namespace Storefront.Pages.Orders;

[Authorize]
public class IndexModel(GatewayClient gateway) : PageModel
{
    public IReadOnlyList<OrderDetails> MyOrders { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            MyOrders = await gateway.GetAsync<List<OrderDetails>>("orders/v1/orders");
        }
        catch (Exception ex) when (ex is GatewayException { StatusCode: not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) } or HttpRequestException)
        {
            ErrorMessage = "Your orders couldn't load because the order service isn't responding. Refresh in a moment.";
        }
    }
}
