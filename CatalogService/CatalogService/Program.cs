using CatalogService.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseSqlite(
        builder.Configuration.GetConnectionString("CatalogDb")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider
        .GetRequiredService<CatalogDbContext>()
        .Database.Migrate();
}


app.UseMiddleware<OnlineStore.Shared.CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
