using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("inventory/v1")]
public class InventoryController(InventoryDbContext context) : ControllerBase
{
    [HttpGet("products/{productId}/stock")]
    public async Task<IActionResult> GetProductStock(int productId)
    {
        var stock = await context.ProductStocks
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (stock == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product stock not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No inventory record exists for product {productId}."
            });
        }

        return Ok(stock);
    }

    [HttpPut("products/{productId}/stock")]
    public async Task<IActionResult> AdjustProductStock(
        int productId,
        AdjustStockRequest request)
    {
        if (request.QuantityChange == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid stock adjustment",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Quantity change cannot be zero."
            });
        }

        var stock = await context.ProductStocks
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (stock == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product stock not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No inventory record exists for product {productId}."
            });
        }

        if (stock.QuantityAvailable + request.QuantityChange < 0)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient stock",
                Status = StatusCodes.Status409Conflict,
                Detail = "Stock adjustment would result in a negative quantity."
            });
        }

        stock.QuantityAvailable += request.QuantityChange;
        stock.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("reservations")]
    public async Task<IActionResult> ReserveStock(ReserveStockRequest request)
    {
        if (request.ProductId < 1 ||
            request.OrderId < 1 ||
            request.Quantity < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid reservation",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Product ID, Order ID, and quantity must be positive."
            });
        }

        var stock = await context.ProductStocks
            .FirstOrDefaultAsync(p => p.ProductId == request.ProductId);

        if (stock == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product stock not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"No inventory record exists for product {request.ProductId}."
            });
        }

        if (stock.QuantityAvailable < request.Quantity)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient stock",
                Status = StatusCodes.Status409Conflict,
                Detail = "There is not enough stock available for this reservation."
            });
        }

        stock.QuantityAvailable -= request.Quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        var reservation = new Reservation
        {
            ProductId = request.ProductId,
            OrderId = request.OrderId,
            Quantity = request.Quantity,
            Status = ReservationStatus.Reserved
        };

        context.Reservations.Add(reservation);

        await context.SaveChangesAsync();

        return Created(
            $"inventory/v1/reservations/{reservation.Id}",
            reservation);
    }

    [HttpPost("reservations/{id:guid}/release")]
    public async Task<IActionResult> ReleaseReservation(Guid id)
    {
        var reservation = await context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Reservation not found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Reservation {id} does not exist."
            });
        }

        if (reservation.Status == ReservationStatus.Released)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Reservation already released",
                Status = StatusCodes.Status409Conflict,
                Detail = "This reservation has already been released."
            });
        }

        var stock = await context.ProductStocks
            .FirstOrDefaultAsync(p => p.ProductId == reservation.ProductId);

        if (stock == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product stock not found",
                Status = StatusCodes.Status404NotFound,
                Detail = "Cannot release reservation because the stock record is missing."
            });
        }

        stock.QuantityAvailable += reservation.Quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        reservation.Status = ReservationStatus.Released;
        reservation.ReleasedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return NoContent();
    }
}
