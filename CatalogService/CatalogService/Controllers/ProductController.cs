using CatalogService.Model;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Controllers
{
    [ApiController]
    [Route("catalog/v1/products")]
    public class ProductController : ControllerBase
    {
        private static readonly List<Product> Products = new()
        {
            new Product
            {
                Id = 1,
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse",
                Price = 799.99,
                StockQuantity = 25
            },
            new Product
            {
                Id = 2,
                Name = "Mechanical Keyboard",
                Description = "RGB mechanical keyboard",
                Price = 2499.99,
                StockQuantity = 15
            }
        };

        // GET /catalog/v1/products
        [HttpGet]
        public ActionResult<IEnumerable<Product>> GetProducts()
        {
            return Ok(Products);
        }

        // GET /catalog/v1/products/{id}
        [HttpGet("{id}")]
        public ActionResult<Product> GetProduct(int id)
        {
            var product = Products.FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with ID {id} was not found."
                });
            }

            return Ok(product);
        }

        // POST /catalog/v1/products
        [HttpPost]
        public ActionResult<Product> CreateProduct(ProductInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Name) ||
                input.Price < 0 ||
                input.StockQuantity < 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Validation error",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Name is required and price and stock quantity cannot be negative."
                });
            }

            var product = new Product
            {
                Id = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1,
                Name = input.Name,
                Description = input.Description,
                Price = input.Price,
                StockQuantity = input.StockQuantity
            };

            Products.Add(product);

            return CreatedAtAction(
                nameof(GetProduct),
                new { id = product.Id },
                product);
        }

        // PUT /catalog/v1/products/{id}
        [HttpPut("{id}")]
        public IActionResult UpdateProduct(int id, ProductInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Name) ||
                input.Price < 0 ||
                input.StockQuantity < 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Validation error",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Name is required and price and stock quantity cannot be negative."
                });
            }

            var product = Products.FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with ID {id} was not found."
                });
            }

            product.Name = input.Name;
            product.Description = input.Description;
            product.Price = input.Price;
            product.StockQuantity = input.StockQuantity;

            return NoContent();
        }

        // DELETE /catalog/v1/products/{id}
        [HttpDelete("{id}")]
        public IActionResult DeleteProduct(int id)
        {
            var product = Products.FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with ID {id} was not found."
                });
            }

            Products.Remove(product);

            return NoContent();
        }
    }
}