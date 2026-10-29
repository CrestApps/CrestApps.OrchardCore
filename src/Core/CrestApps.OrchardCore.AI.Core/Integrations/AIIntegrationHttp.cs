using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// Reads integration answers and turns refusals into errors that say which provider refused and why.
/// </summary>
public static class AIIntegrationHttp
{
    /// <summary>
    /// Reads a JSON answer.
    /// </summary>
    /// <param name="response">The answer.</param>
    /// <param name="provider">The provider's name, for the error.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="HttpRequestException">The provider refused.</exception>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{provider} answered {(int)response.StatusCode}: {(body.Length > 500 ? body[..500] : body)}", null, response.StatusCode);
        }

        return string.IsNullOrWhiteSpace(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>
    /// Reads a string property.
    /// </summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static string String(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
