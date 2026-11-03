using System.Net.Http.Headers;

namespace Storefront.Services;

public static class AuthClaims
{
    public const string AccessToken = "access_token";
}

/// Attaches the logged-in user's JWT to every call the Storefront makes to the gateway.
public class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = accessor.HttpContext?.User.FindFirst(AuthClaims.AccessToken)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
