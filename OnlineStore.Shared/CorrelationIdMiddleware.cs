using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OnlineStore.Shared;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(
        HttpContext context,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        Correlation.Set(correlationId);

        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope("CorrelationId: {CorrelationId}", correlationId))
        {
            logger.LogInformation(
                "Received request with Correlation ID: {CorrelationId}",
                correlationId);

            await next(context);
        }
    }
}
