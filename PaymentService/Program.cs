using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Shared;
using PaymentService.Data;
using PaymentService.Messaging;
using PaymentService.Simulation;
using Store.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; });

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PaymentDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("PaymentDb")));

// Simulate-payment-failure switch (saga compensation on demand)
builder.Services.AddSingleton<PaymentFailureSwitch>();

// RabbitMQ (Module 4)
builder.Services.AddRabbitMessaging(builder.Configuration);
builder.Services.AddHostedService<StockReservedConsumer>();
builder.Services.AddHostedService<RefundRequestedConsumer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.Migrate();   // creates DB on a clean checkout

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
