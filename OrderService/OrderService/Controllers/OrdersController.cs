using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using Store.Contracts;
using Store.Messaging;
using CatalogApiClient = OrderService.Clients.CatalogApiClient;
using CatalogApiException = OrderService.Clients.ApiException;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace OrderService.Controllers;

[ApiController]
[Route("orders/v1/orders")]
public class OrdersController(
    OrderDbContext db,
    CatalogApiClient catalogClient,
    IEventPublisher publisher,
    ILogger<OrdersController> log) : ControllerBase
{
    // Forwarded by the gateway from the JWT "sub" claim.
    private string? CurrentUserId =>
        Request.Headers.TryGetValue("X-User-Id", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : null;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDetails>>> GetOrders(CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (CurrentUserId is { } userId)
            query = query.Where(o => o.CustomerId == userId);

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync(ct);
        var ids = orders.Select(o => o.Id).ToList();
        var history = await db.OrderStatusHistories.AsNoTracking()
            .Where(h => ids.Contains(h.OrderId))
            .ToListAsync(ct);

        return Ok(orders.Select(o => ToDetails(o, history.Where(h => h.OrderId == o.Id))).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetails>> GetOrderById(int id, CancellationToken ct)
    {
        var order = await FindOwnedAsync(id, ct);
        if (order is null) return OrderNotFound(id);

        var history = await db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.OrderId == id)
            .ToListAsync(ct);
        return Ok(ToDetails(order, history));
    }

    [HttpGet("{id:int}/items")]
    public async Task<ActionResult<IEnumerable<OrderItemDetails>>> GetOrderItems(int id, CancellationToken ct)
    {
        var order = await FindOwnedAsync(id, ct);
        if (order is null) return OrderNotFound(id);
        return Ok(order.Items.Select(i => new OrderItemDetails(i.ProductId, i.Quantity, i.UnitPrice)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<OrderDetails>> CreateOrder(OrderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName) ||
            input.Items == null ||
            input.Items.Count == 0 ||
            input.Items.Any(item => item.ProductId < 1 || item.Quantity < 1))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Customer name and at least one valid order item are required."
            });
        }

        // Step 1 (synchronous, Module 3): validate products and prices through Catalog.
        var items = new List<OrderItem>();
        foreach (var inputItem in input.Items)
        {
            try
            {
                var product = await catalogClient.GetProductByIdAsync(inputItem.ProductId);
                items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = inputItem.Quantity,
                    UnitPrice = (decimal)product.Price
                });
            }
            catch (CatalogApiException ex) when (ex.StatusCode == 404)
            {
                return Problem(statusCode: 404, title: "Product not found",
                    detail: $"Product with ID {inputItem.ProductId} does not exist in Catalog.");
            }
            catch (CatalogApiException)
            {
                return Problem(statusCode: 502, title: "Catalog Service error",
                    detail: "Unable to retrieve product information from Catalog Service.");
            }
            catch (HttpRequestException)
            {
                return Problem(statusCode: 502, title: "Catalog Service unavailable",
                    detail: "Catalog Service could not be reached.");
            }
            catch (TaskCanceledException)
            {
                return Problem(statusCode: 504, title: "Catalog Service timeout",
                    detail: "Catalog Service did not respond in time.");
            }
            catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("Polly") == true)
            {
                return Problem(statusCode: 503, title: "Catalog Service unavailable",
                    detail: "Catalog Service is temporarily unavailable (circuit open or timed out).");
            }
        }

        // Step 2 (local transaction): save the order as Pending with its first history entry.
        var order = new Order
        {
            CustomerId = CurrentUserId ?? "anonymous",
            CustomerName = input.CustomerName.Trim(),
            Items = items,
            TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice),
            Status = OrderStatuses.Pending
        };

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id, Status = OrderStatuses.Pending, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // Step 3: start the saga. Inventory, Payment and this service continue it over RabbitMQ.
        try
        {
            publisher.Publish(RoutingKeys.OrderPlaced, new OrderPlaced(
                order.Id,
                order.CustomerId,
                items.Select(i => new OrderLine(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
                order.TotalAmount));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not publish OrderPlaced for order {OrderId}; cancelling it", order.Id);
            order.Status = OrderStatuses.Cancelled;
            order.FailureReason = "Order processing could not start.";
            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id, Status = OrderStatuses.Cancelled, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return Problem(statusCode: 503, title: "Order processing unavailable",
                detail: "The order was cancelled because the message broker could not be reached. Try again shortly.");
        }

        log.LogInformation("Order {OrderId} placed for customer {CustomerId}; saga started", order.Id, order.CustomerId);

        var startHistory = await db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.OrderId == order.Id)
            .ToListAsync(ct);
        return AcceptedAtAction(nameof(GetOrderById), new { id = order.Id }, ToDetails(order, startHistory));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrder(int id, OrderInput input, CancellationToken ct)
    {
        var order = await FindOwnedAsync(id, ct);
        if (order is null) return OrderNotFound(id);

        return Problem(statusCode: 409, title: "Order cannot be changed",
            detail: "Orders are processed as soon as they are placed. Cancel and place a new order instead.");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(
            o => o.Id == id && (CurrentUserId == null || o.CustomerId == CurrentUserId), ct);
        if (order is null) return OrderNotFound(id);

        if (order.Status is not (OrderStatuses.Confirmed or OrderStatuses.Cancelled))
            return Problem(statusCode: 409, title: "Order is still processing",
                detail: "Wait until the order is Confirmed or Cancelled before deleting it.");

        db.OrderStatusHistories.RemoveRange(db.OrderStatusHistories.Where(h => h.OrderId == id));
        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<Order?> FindOwnedAsync(int id, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && (CurrentUserId == null || o.CustomerId == CurrentUserId), ct);

    private ObjectResult OrderNotFound(int id) =>
        Problem(statusCode: 404, title: "Resource not found", detail: $"Order with ID {id} was not found.");

    private static OrderDetails ToDetails(Order o, IEnumerable<OrderStatusHistory> history) => new(
        o.Id,
        o.CustomerId,
        o.CustomerName,
        o.Status,
        o.FailureReason,
        o.TotalAmount,
        o.CreatedAt,
        o.Items.Select(i => new OrderItemDetails(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
        history.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new OrderStatusEntry(h.Status, h.CreatedAt)).ToList());
}
