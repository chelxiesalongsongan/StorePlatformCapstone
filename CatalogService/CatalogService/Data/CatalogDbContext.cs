using CatalogService.Model;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}