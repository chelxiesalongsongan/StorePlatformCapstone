using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("orders/v1/orders")]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> Orders = new()
    {
        new Order
        {
            Id = 1,
            CustomerName = "Juan Dela Cruz",
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    Id = 1,
                    ProductId = 1,
                    Quantity = 2,
                    UnitPrice = 799.99
                }
            },
            TotalAmount = 1599.98
        }
    };

    // GET /orders/v1/orders
    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetOrders()
    {
        return Ok(Orders);
    }

    // GET /orders/v1/orders/{id}
    [HttpGet("{id}")]
    public ActionResult<Order> GetOrderById(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Order with ID {id} was not found."
            });
        }

        return Ok(order);
    }

    // POST /orders/v1/orders
    [HttpPost]
    public ActionResult<Order> CreateOrder(OrderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName) ||
            input.Items == null ||
            input.Items.Count == 0 ||
            input.Items.Any(item =>
                item.ProductId < 1 ||
                item.Quantity < 1 ||
                item.UnitPrice < 0))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Customer name and at least one valid order item are required."
            });
        }

        var newOrderId = Orders.Count == 0
            ? 1
            : Orders.Max(o => o.Id) + 1;

        var items = new List<OrderItem>();
        var itemId = 1;

        foreach (var inputItem in input.Items)
        {
            items.Add(new OrderItem
            {
                Id = itemId++,
                ProductId = inputItem.ProductId,
                Quantity = inputItem.Quantity,
                UnitPrice = inputItem.UnitPrice
            });
        }

        var totalAmount = items.Sum(item =>
            item.Quantity * item.UnitPrice);

        var order = new Order
        {
            Id = newOrderId,
            CustomerName = input.CustomerName,
            Items = items,
            TotalAmount = totalAmount
        };

        Orders.Add(order);

        return CreatedAtAction(
            nameof(GetOrderById),
            new { id = order.Id },
            order);
    }

    // PUT /orders/v1/orders/{id}
    [HttpPut("{id}")]
    public IActionResult UpdateOrder(int id, OrderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName) ||
            input.Items == null ||
            input.Items.Count == 0 ||
            input.Items.Any(item =>
                item.ProductId < 1 ||
                item.Quantity < 1 ||
                item.UnitPrice < 0))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Customer name and at least one valid order item are required."
            });
        }

        var order = Orders.FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Order with ID {id} was not found."
            });
        }

        var items = new List<OrderItem>();
        var itemId = 1;

        foreach (var inputItem in input.Items)
        {
            items.Add(new OrderItem
            {
                Id = itemId++,
                ProductId = inputItem.ProductId,
                Quantity = inputItem.Quantity,
                UnitPrice = inputItem.UnitPrice
            });
        }

        order.CustomerName = input.CustomerName;
        order.Items = items;
        order.TotalAmount = items.Sum(item =>
            item.Quantity * item.UnitPrice);

        return NoContent();
    }

    // DELETE /orders/v1/orders/{id}
    [HttpDelete("{id}")]
    public IActionResult DeleteOrder(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Order with ID {id} was not found."
            });
        }

        Orders.Remove(order);

        return NoContent();
    }

    // GET /orders/v1/orders/{id}/items
    [HttpGet("{id}/items")]
    public ActionResult<IEnumerable<OrderItem>> GetOrderItems(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Resource not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Order with ID {id} was not found."
            });
        }

        return Ok(order.Items);
    }
}