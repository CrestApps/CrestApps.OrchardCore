using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Default implementation of <see cref="IDialpadWebhookApiService"/> that calls the Dialpad Admin API using
/// the shared resilient Dialpad HTTP client.
/// </summary>
public sealed class DialpadWebhookApiService : IDialpadWebhookApiService
{
    private static readonly string[] _callStates =
    [
        "calling",
        "preanswer",
        "ringing",
        "connected",
        "hold",
        "hangup",
        "missed",
        "voicemail",
        "recording",
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadWebhookApiService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public DialpadWebhookApiService(
        IHttpClientFactory httpClientFactory,
        ILogger<DialpadWebhookApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DialpadWebhookRegistrationResult> CreateAsync(
        string baseUrl,
        string bearerToken,
        string webhookUrl,
        string signingSecret,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(baseUrl, bearerToken);

        var webhookId = await CreateWebhookAsync(client, webhookUrl, signingSecret, cancellationToken);

        if (string.IsNullOrEmpty(webhookId))
        {
            return null;
        }

        if (!long.TryParse(webhookId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endpointId))
        {
            _logger.LogError("Dialpad returned webhook id {WebhookId}, which cannot be used as a call-event subscription endpoint id.", webhookId.SanitizeLogValue());

            await DeleteResourceAsync(client, $"webhooks/{webhookId}", "webhook", cancellationToken);

            return null;
        }

        var subscriptionId = await CreateCallEventSubscriptionAsync(client, endpointId, cancellationToken);

        if (string.IsNullOrEmpty(subscriptionId))
        {
            await DeleteResourceAsync(client, $"webhooks/{webhookId}", "webhook", cancellationToken);

            return null;
        }

        return new DialpadWebhookRegistrationResult(webhookId, subscriptionId);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        string baseUrl,
        string bearerToken,
        string webhookId,
        string callEventSubscriptionId,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(baseUrl, bearerToken);

        if (!string.IsNullOrEmpty(callEventSubscriptionId) &&
            !await DeleteResourceAsync(client, $"subscriptions/call/{callEventSubscriptionId}", "call-event subscription", cancellationToken))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(webhookId) &&
            !await DeleteResourceAsync(client, $"webhooks/{webhookId}", "webhook", cancellationToken))
        {
            return false;
        }

        return true;
    }

    private HttpClient CreateClient(string baseUrl, string bearerToken)
    {
        var client = _httpClientFactory.CreateClient(DialpadConstants.ProviderTechnicalName);
        var resolvedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : baseUrl + '/';

        client.BaseAddress = new Uri(resolvedBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return client;
    }

    private async Task<string> CreateWebhookAsync(
        HttpClient client,
        string webhookUrl,
        string secret,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("webhooks", new
        {
            hook_url = webhookUrl,
            secret,
        }, cancellationToken);

        return await ReadDialpadIdAsync(response, "webhook", cancellationToken);
    }

    private async Task<string> CreateCallEventSubscriptionAsync(
        HttpClient client,
        long endpointId,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("subscriptions/call", new
        {
            endpoint_id = endpointId,
            enabled = true,
            call_states = _callStates,
        }, cancellationToken);

        return await ReadDialpadIdAsync(response, "call-event subscription", cancellationToken);
    }

    private async Task<bool> DeleteResourceAsync(
        HttpClient client,
        string requestUri,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var response = await client.DeleteAsync(requestUri, cancellationToken);
        var payload = await SafeReadContentAsync(response, cancellationToken);

        if (response.IsSuccessStatusCode || (int)response.StatusCode == 404)
        {
            return true;
        }

        _logger.LogError(
            "Dialpad rejected the {ResourceName} deletion request with status code {StatusCode}. Response: {Response}",
            resourceName,
            response.StatusCode,
            payload.SanitizeLogValue());

        return false;
    }

    private async Task<string> ReadDialpadIdAsync(
        HttpResponseMessage response,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var payload = await SafeReadContentAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Dialpad rejected the {ResourceName} registration request with status code {StatusCode}. Response: {Response}",
                resourceName,
                response.StatusCode,
                payload.SanitizeLogValue());

            return null;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogError("Dialpad returned an empty {ResourceName} registration response.", resourceName);

            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("id", out var idElement))
            {
                if (idElement.ValueKind == JsonValueKind.String)
                {
                    return idElement.GetString();
                }

                return idElement.GetRawText();
            }
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Dialpad returned an invalid {ResourceName} registration response.", resourceName);
        }

        return null;
    }

    private static async Task<string> SafeReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
