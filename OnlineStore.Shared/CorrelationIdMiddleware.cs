using Microsoft.AspNetCore.Http;

namespace OnlineStore.Shared;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        Correlation.Set(correlationId);

        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
