using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Storefront.Services;

public class GatewayException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// The Storefront's only way to reach the backend: every call goes through the API gateway.
/// Non-success responses throw GatewayException with the Problem Details message.
public class GatewayClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Web, ct))!;
    }

    public async Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(path, body, Web, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Web, ct))!;
    }

    public async Task PostAsync(string path, object body, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(path, body, Web, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task PutAsync(string path, object body, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync(path, body, Web, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.ReasonPhrase ?? response.StatusCode.ToString();
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var problem = JsonSerializer.Deserialize<ProblemDetails>(text, Web);
                message = problem?.Detail ?? problem?.Title ?? message;
            }
            catch (JsonException)
            {
                message = text; // e.g. the gateway's plain-text 429 message
            }
        }
        throw new GatewayException(response.StatusCode, message);
    }
}
