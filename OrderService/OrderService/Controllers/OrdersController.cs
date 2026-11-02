using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using CatalogApiClient = OrderService.Clients.CatalogApiClient;
using InventoryApiClient = InventoryService.Clients.InventoryApiClient;
using InventoryApiException = InventoryService.Clients.ApiException;
using InventoryReserveStockRequest = InventoryService.Clients.ReserveStockRequest;
using CatalogApiException = OrderService.Clients.ApiException;
using PaymentApiClient = PaymentService.Clients.PaymentApiClient;
using PaymentApiException = PaymentService.Clients.ApiException;
using PaymentChargeRequest = PaymentService.Clients.ChargeRequest;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using OrderService.Clients.Contracts;

namespace OrderService.Controllers;

[ApiController]
[Route("orders/v1/orders")]
public class OrdersController(CatalogApiClient catalogClient, InventoryApiClient inventoryClient, PaymentApiClient paymentClient) : ControllerBase
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

    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetOrders()
    {
        return Ok(Orders);
    }

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

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(OrderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName) ||
            input.Items == null ||
            input.Items.Count == 0 ||
            input.Items.Any(item =>
                item.ProductId < 1 ||
                item.Quantity < 1))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Customer name and at least one valid order item are required."
            });
        }

        var items = new List<OrderItem>();
        var itemId = 1;

        // Step 1: Validate products through Catalog Service.
        foreach (var inputItem in input.Items)
        {
            try
            {
                var product = await catalogClient.GetProductByIdAsync(inputItem.ProductId);

                items.Add(new OrderItem
                {
                    Id = itemId++,
                    ProductId = product.Id,
                    Quantity = inputItem.Quantity,
                    UnitPrice = product.Price
                });
            }
            catch (CatalogApiException ex) when (ex.StatusCode == 404)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with ID {inputItem.ProductId} does not exist in Catalog."
                });
            }
            catch (CatalogApiException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "Catalog Service error",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = "Unable to retrieve product information from Catalog Service."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "Catalog Service unavailable",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = "Catalog Service could not be reached."
                });
            }
            catch (TaskCanceledException)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
                {
                    Title = "Catalog Service timeout",
                    Status = StatusCodes.Status504GatewayTimeout,
                    Detail = "Catalog Service did not respond in time."
                });
            }
        }

        var newOrderId = Orders.Count == 0
            ? 1
            : Orders.Max(o => o.Id) + 1;

        var reservationIds = new List<Guid>();

        // Step 2: Reserve stock through Inventory Service.
        foreach (var item in items)
        {
            try
            {
                var reservation = await inventoryClient.ReserveStockAsync(
                    new InventoryReserveStockRequest
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        OrderId = newOrderId
                    });

                reservationIds.Add(reservation.Id);
            }
            catch (InventoryApiException ex)
            {
                // Step 3: Compensation - release previous reservations.
                foreach (var reservationId in reservationIds)
                {
                    try
                    {
                        await inventoryClient.ReleaseReservationAsync(reservationId);
                    }
                    catch
                    {
                        // Continue attempting to release the remaining reservations.
                    }
                }

                var statusCode = ex.StatusCode == 404
                    ? StatusCodes.Status404NotFound
                    : ex.StatusCode == 409
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status502BadGateway;

                return StatusCode(statusCode, new ProblemDetails
                {
                    Title = "Inventory Service error",
                    Status = statusCode,
                    Detail = ex.StatusCode == 409
                        ? "Insufficient stock. Previous stock reservations were released."
                        : "Unable to complete stock reservation. Previous stock reservations were released."
                });
            }
            catch (HttpRequestException)
            {
                foreach (var reservationId in reservationIds)
                {
                    try
                    {
                        await inventoryClient.ReleaseReservationAsync(reservationId);
                    }
                    catch
                    {
                        // Continue attempting to release the remaining reservations.
                    }
                }

                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "Inventory Service unavailable",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = "Inventory Service could not be reached. Previous stock reservations were released."
                });
            }
            catch (TaskCanceledException)
            {
                foreach (var reservationId in reservationIds)
                {
                    try
                    {
                        await inventoryClient.ReleaseReservationAsync(reservationId);
                    }
                    catch
                    {
                        // Continue attempting to release the remaining reservations.
                    }
                }

                return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
                {
                    Title = "Inventory Service timeout",
                    Status = StatusCodes.Status504GatewayTimeout,
                    Detail = "Inventory Service did not respond in time. Previous stock reservations were released."
                });
            }
        }

        // Step 4: Charge payment through Payment Service.
        var totalAmount = items.Sum(item => item.Quantity * item.UnitPrice);

        PaymentService.Clients.Payment payment;

        try
        {
            payment = await paymentClient.ChargePaymentAsync(
                new PaymentChargeRequest
                {
                    OrderId = newOrderId,
                    Amount = totalAmount,
                    ForceFailure = input.ForcePaymentFailure
                });
        }
        catch (PaymentApiException ex)
        {
            // Compensation: release all stock reservations if payment fails.
            foreach (var reservationId in reservationIds)
            {
                try
                {
                    await inventoryClient.ReleaseReservationAsync(reservationId);
                }
                catch
                {
                    // Continue attempting to release the remaining reservations.
                }
            }

            var statusCode = ex.StatusCode == 402
                ? StatusCodes.Status402PaymentRequired
                : StatusCodes.Status502BadGateway;

            return StatusCode(statusCode, new ProblemDetails
            {
                Title = ex.StatusCode == 402
                    ? "Payment declined"
                    : "Payment Service error",
                Status = statusCode,
                Detail = ex.StatusCode == 402
                    ? "Payment was declined. All previous stock reservations were released."
                    : "Payment Service could not complete the transaction. Stock reservations were released."
            });
        }
        catch (HttpRequestException)
        {
            foreach (var reservationId in reservationIds)
            {
                try
                {
                    await inventoryClient.ReleaseReservationAsync(reservationId);
                }
                catch
                {
                    // Continue attempting to release the remaining reservations.
                }
            }

            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Payment Service unavailable",
                Status = StatusCodes.Status502BadGateway,
                Detail = "Payment Service could not be reached. Stock reservations were released."
            });
        }
        catch (TaskCanceledException)
        {
            foreach (var reservationId in reservationIds)
            {
                try
                {
                    await inventoryClient.ReleaseReservationAsync(reservationId);
                }
                catch
                {
                    // Continue attempting to release the remaining reservations.
                }
            }

            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Title = "Payment Service timeout",
                Status = StatusCodes.Status504GatewayTimeout,
                Detail = "Payment Service did not respond in time. Stock reservations were released."
            });
        }
        // Step 4: Save the order only after all reservations succeed.
        var order = new Order
        {
            Id = newOrderId,
            CustomerName = input.CustomerName,
            Items = items,
            TotalAmount = totalAmount,
            ReservationIds = reservationIds,
            PaymentId = payment.Id,
            PaymentStatus = payment.Status.ToString()
        };

        Orders.Add(order);

        return CreatedAtAction(
            nameof(GetOrderById),
            new { id = order.Id },
            order);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrder(int id, OrderInput input)
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

        if (string.IsNullOrWhiteSpace(input.CustomerName) ||
            input.Items == null ||
            input.Items.Count == 0 ||
            input.Items.Any(item =>
                item.ProductId < 1 ||
                item.Quantity < 1))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Customer name and at least one valid order item are required."
            });
        }

        var items = new List<OrderItem>();
        var itemId = 1;

        foreach (var inputItem in input.Items)
        {
            Product product;

            try
            {
                product = await catalogClient.GetProductByIdAsync(inputItem.ProductId);
            }
            catch (CatalogApiException ex) when (ex.StatusCode == 404)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with ID {inputItem.ProductId} does not exist in Catalog."
                });
            }
            catch (CatalogApiException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "Catalog Service error",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = "Unable to retrieve product information from Catalog Service."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "Catalog Service unavailable",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = "Catalog Service could not be reached."
                });
            }

            items.Add(new OrderItem
            {
                Id = itemId++,
                ProductId = product.Id,
                Quantity = inputItem.Quantity,
                UnitPrice = product.Price
            });
        }

        order.CustomerName = input.CustomerName;
        order.Items = items;
        order.TotalAmount = items.Sum(item => item.Quantity * item.UnitPrice);

        return NoContent();
    }

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
