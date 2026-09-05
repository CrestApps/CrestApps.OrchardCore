using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Infrastructure;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Telnyx implementation of <see cref="ISmsProvider"/>. It sends outbound messages through the Telnyx
/// Messaging API (<c>POST /v2/messages</c>), reading its resolved credentials from
/// <see cref="IOptionsMonitor{TOptions}"/> of <see cref="TelnyxSmsOptions"/> (merged appsettings + UI settings),
/// mirroring OrchardCore's Twilio provider structure. Registered under the technical name "Telnyx".
/// </summary>
public sealed class TelnyxSmsProvider : ISmsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TelnyxSmsOptions> _options;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsProvider"/> class.
    /// </summary>
    public TelnyxSmsProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TelnyxSmsOptions> options,
        ILogger<TelnyxSmsProvider> logger,
        IStringLocalizer<TelnyxSmsProvider> stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public LocalizedString Name => S["Telnyx"];

    /// <inheritdoc/>
    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.To))
        {
            return Failed("The recipient (To) is required.");
        }

        var options = _options.CurrentValue;

        if (!options.IsValid)
        {
            _logger.LogError("Unable to send a Telnyx SMS because the Telnyx SMS provider is not configured or enabled.");

            return Failed("The Telnyx SMS provider is not configured.");
        }

        // A from-number is optional when a messaging profile is configured: Telnyx selects the sender from the
        // profile's number pool. This lets automated flows send without a per-message channel endpoint. Without a
        // messaging profile, an explicit From is required because Telnyx has no pool to choose from.
        if (string.IsNullOrEmpty(message.From) && string.IsNullOrEmpty(options.MessagingProfileId))
        {
            return Failed("A sending number (From) is required unless a Telnyx messaging profile is configured.");
        }

        var payload = new Dictionary<string, object>
        {
            ["to"] = message.To,
            ["text"] = message.Body ?? string.Empty,
        };

        if (!string.IsNullOrEmpty(message.From))
        {
            payload["from"] = message.From;
        }

        if (!string.IsNullOrEmpty(options.MessagingProfileId))
        {
            payload["messaging_profile_id"] = options.MessagingProfileId;
        }

        using var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.ApiBaseUrl) ? TelnyxConstants.DefaultApiBaseUrl : options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        try
        {
            using var response = await client.PostAsJsonAsync(TelnyxConstants.MessagesPath, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogWarning("Telnyx SMS send failed with status {StatusCode}. Response: {Response}", (int)response.StatusCode, Truncate(errorBody));

                return Failed($"The Telnyx messaging API returned {(int)response.StatusCode}: {Truncate(errorBody)}");
            }

            var providerMessageId = await TryReadMessageIdAsync(response, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information) && !string.IsNullOrEmpty(providerMessageId))
            {
                _logger.LogInformation("Telnyx accepted an outbound SMS. Provider message id: {ProviderMessageId}", providerMessageId);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "The Telnyx messaging API request failed.");

            return Failed("The Telnyx messaging API request failed.");
        }
    }

    private static async Task<string> TryReadMessageIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return document.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) || value.Length <= 500 ? value : value[..500];

    private Result Failed(string message) => Result.Failed(S[message]);
}
