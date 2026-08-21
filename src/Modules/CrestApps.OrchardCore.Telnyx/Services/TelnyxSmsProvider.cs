using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Infrastructure;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Telnyx implementation of <see cref="ISmsProvider"/>. It sends outbound messages through the Telnyx
/// Messaging API (<c>POST /v2/messages</c>) authenticated with the account API key already stored (protected)
/// in the Telnyx provider settings. Registered under the technical name "Telnyx" so the SMS portal's
/// dispatcher can resolve it per number.
/// </summary>
public sealed class TelnyxSmsProvider : ISmsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsProvider"/> class.
    /// </summary>
    public TelnyxSmsProvider(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TelnyxSmsProvider> logger,
        IStringLocalizer<TelnyxSmsProvider> stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public LocalizedString Name => S["Telnyx"];

    /// <inheritdoc/>
    public async Task<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.To) || string.IsNullOrEmpty(message.From))
        {
            return Failed("Both the sending number (From) and the recipient (To) are required.");
        }

        var settings = await _siteService.GetSettingsAsync<TelnyxSettings>();

        if (!settings.IsEnabled)
        {
            return Failed("The Telnyx provider is disabled.");
        }

        if (string.IsNullOrEmpty(settings.ApiKey) || !TryUnprotect(settings.ApiKey, out var apiKey))
        {
            _logger.LogError("Unable to send a Telnyx SMS because the API key is missing or could not be unprotected.");

            return Failed("The Telnyx API key is not configured.");
        }

        var smsSettings = await _siteService.GetSettingsAsync<TelnyxSmsSettings>();

        var payload = new Dictionary<string, object>
        {
            ["from"] = message.From,
            ["to"] = message.To,
            ["text"] = message.Body ?? string.Empty,
        };

        if (!string.IsNullOrEmpty(smsSettings.MessagingProfileId))
        {
            payload["messaging_profile_id"] = smsSettings.MessagingProfileId;
        }

        using var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
            ? TelnyxConstants.DefaultApiBaseUrl
            : settings.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = await client.PostAsJsonAsync(TelnyxConstants.MessagesPath, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogWarning("Telnyx SMS send failed with status {StatusCode}.", (int)response.StatusCode);

                return Failed($"The Telnyx messaging API returned {(int)response.StatusCode}: {Truncate(errorBody)}");
            }

            // The Telnyx message id (data.id) correlates delivery receipts. The ISmsProvider contract returns
            // only a Result, so the id is logged here; the SMS portal's delivery webhook correlates receipts by
            // (from, to, latest non-terminal outbound) and pins the id on first receipt.
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

    private bool TryUnprotect(string protectedValue, out string value)
    {
        value = null;

        try
        {
            value = _dataProtectionProvider.CreateProtector(TelnyxConstants.ProtectorName).Unprotect(protectedValue);

            return !string.IsNullOrEmpty(value);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) || value.Length <= 500 ? value : value[..500];

    private Result Failed(string message) => Result.Failed(S[message]);
}
