using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;
using OrchardCore.Sms;
using OrchardCore.Sms.Models;
using OrchardCore.Sms.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Sends picture messages through Twilio. OrchardCore's Twilio provider sends text only, so a picture message goes to
/// the same Twilio Messages API with the same account, adding a <c>MediaUrl</c> for each picture; Twilio fetches each
/// picture from its link itself. It also signs the download of a picture a customer sent, for an account that serves
/// its media only to itself.
/// </summary>
public sealed class TwilioSmsMediaSender : ISmsMediaSender, IMessagingMediaRequestAuthenticator
{
    /// <summary>
    /// The name of the HTTP client the Twilio API is called with.
    /// </summary>
    public const string HttpClientName = "CrestApps.Omnichannel.Messaging.Sms.Twilio";

    // Twilio's "Attempt to send to unsubscribed recipient": the number texted STOP to this sender.
    private const int UnsubscribedErrorCode = 21610;

    private const string ApiHost = "api.twilio.com";

    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwilioSmsMediaSender"/> class.
    /// </summary>
    public TwilioSmsMediaSender(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<TwilioSmsMediaSender> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderName => TwilioSmsProvider.TechnicalName;

    /// <inheritdoc/>
    public async Task<MessageDispatchResult> SendAsync(SmsMessage message, IReadOnlyList<string> mediaUrls, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var (accountSid, authToken, defaultNumber) = await GetCredentialsAsync();

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
        {
            _logger.LogError("Unable to send a Twilio picture message because the Twilio account SID or auth token is not configured.");

            return MessageDispatchResult.Failed("The Twilio SMS provider is not configured.");
        }

        var from = string.IsNullOrEmpty(message.From) ? defaultNumber : message.From;

        var fields = new List<KeyValuePair<string, string>>
        {
            new("To", message.To),
            new("From", from),
        };

        if (!string.IsNullOrEmpty(message.Body))
        {
            fields.Add(new("Body", message.Body));
        }

        foreach (var url in mediaUrls ?? [])
        {
            fields.Add(new("MediaUrl", url));
        }

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{ApiHost}/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(fields),
        };

        request.Headers.Authorization = BasicAuthentication(accountSid, authToken);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (ReadErrorCode(body) == UnsubscribedErrorCode)
                {
                    var optedOut = MessageDispatchResult.Failed("The recipient has opted out of messages from this number.");
                    optedOut.ErrorCode = OmnichannelConstants.SmsErrorCodes.RecipientOptedOut;

                    return optedOut;
                }

                _logger.LogWarning("Twilio refused a picture message with status {StatusCode}. Response: {Response}", (int)response.StatusCode, Truncate(body).SanitizeLogValue());

                return MessageDispatchResult.Failed($"The Twilio messaging API returned {(int)response.StatusCode}: {Truncate(body)}");
            }

            var sid = ReadSid(body);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Twilio accepted a picture message with {Count} picture(s). Message SID: {MessageSid}", mediaUrls?.Count ?? 0, sid);
            }

            return MessageDispatchResult.Success(sid);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "The Twilio messaging API request for a picture message failed.");

            return MessageDispatchResult.Failed("The Twilio messaging API request failed.");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryAuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Only Twilio's own API host is given the account's credentials. Twilio answers there with a redirect to its
        // media store, and the client drops the Authorization header when it follows it.
        if (request.RequestUri is null || !string.Equals(request.RequestUri.Host, ApiHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var (accountSid, authToken, _) = await GetCredentialsAsync();

        if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
        {
            return false;
        }

        request.Headers.Authorization = BasicAuthentication(accountSid, authToken);

        return true;
    }

    private async Task<(string AccountSid, string AuthToken, string PhoneNumber)> GetCredentialsAsync()
    {
        var settings = await _siteService.GetSettingsAsync<TwilioSettings>();

        if (settings is null || string.IsNullOrEmpty(settings.AccountSID) || string.IsNullOrEmpty(settings.AuthToken))
        {
            return (null, null, null);
        }

        try
        {
            var authToken = _dataProtectionProvider.CreateProtector(TwilioSmsProvider.ProtectorName).Unprotect(settings.AuthToken);

            return (settings.AccountSID, authToken, settings.PhoneNumber);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "The saved Twilio auth token could not be decrypted.");

            return (null, null, null);
        }
    }

    private static AuthenticationHeaderValue BasicAuthentication(string accountSid, string authToken)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));

    private static string ReadSid(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value)
        => string.IsNullOrEmpty(value) || value.Length <= 500 ? value : value[..500];
}
