namespace OnlineStore.Shared;

public class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("X-Correlation-ID"))
        {
            request.Headers.TryAddWithoutValidation(
                "X-Correlation-ID",
                Correlation.Current);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
