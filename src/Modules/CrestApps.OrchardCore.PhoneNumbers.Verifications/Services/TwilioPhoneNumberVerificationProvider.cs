using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Verifies phone numbers using Twilio Lookup and maps the native response into the
/// provider-agnostic <see cref="PhoneNumberVerificationResult"/>.
/// </summary>
public sealed class TwilioPhoneNumberVerificationProvider : IPhoneNumberVerificationProvider
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Twilio";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwilioPhoneNumberVerificationProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service used to read provider settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to decrypt secrets.</param>
    /// <param name="phoneNumberService">The phone number service used to resolve time zones.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public TwilioPhoneNumberVerificationProvider(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<TwilioPhoneNumberVerificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _phoneNumberService = phoneNumberService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PhoneNumberVerificationResult> VerifyAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        var settings = await _siteService.GetSettingsAsync<TwilioPhoneNumberVerificationSettings>();
        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var credentials = ResolveCredentials(settings, protector);

        if (credentials is null)
        {
            _logger.LogWarning("Twilio Lookup credentials are not configured. Skipping phone number verification.");

            return CreateFailedResult(phoneNumber, null, "Twilio Lookup credentials are not configured.");
        }

        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? TwilioPhoneNumberVerificationSettings.DefaultEndpoint
            : settings.Endpoint;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildRequestUri(endpoint, phoneNumber, settings.CountryCode, settings.Fields));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var client = _httpClientFactory.CreateClient(nameof(TwilioPhoneNumberVerificationProvider));

        PhoneNumberVerificationProviderLogMessages.Starting(
            _logger,
            "Twilio Lookup",
            request.RequestUri,
            settings.AuthenticationType.ToString(),
            credentials is not null);

        using var response = await client.SendAsync(request, cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        PhoneNumberVerificationProviderLogMessages.ResponseReceived(
            _logger,
            "Twilio Lookup",
            (int)response.StatusCode,
            payload?.Length ?? 0);

        if (!response.IsSuccessStatusCode)
        {
            PhoneNumberVerificationProviderLogMessages.NonSuccessStatusCode(
                _logger,
                "Twilio Lookup",
                (int)response.StatusCode,
                response.ReasonPhrase);

            return CreateFailedResult(phoneNumber, payload, $"Twilio Lookup returned HTTP status code {(int)response.StatusCode}.");
        }

        TwilioLookupResponse parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<TwilioLookupResponse>(
                payload,
                PhoneNumberVerificationProviderJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse the Twilio Lookup response.");

            return CreateFailedResult(phoneNumber, payload, "Failed to parse the Twilio Lookup response.");
        }

        if (parsed is null)
        {
            return CreateFailedResult(phoneNumber, payload, "The Twilio Lookup response was empty.");
        }

        var result = MapResponse(phoneNumber, settings.CountryCode, parsed, payload, _clock.UtcNow, _phoneNumberService);

        PhoneNumberVerificationProviderLogMessages.Completed(_logger, "Twilio Lookup", result);

        return result;
    }

    internal static PhoneNumberVerificationResult MapResponse(
        string phoneNumber,
        string countryCode,
        TwilioLookupResponse parsed,
        string payload,
        DateTime verificationDateUtc,
        IPhoneNumberService phoneNumberService)
    {
        var lineType = MapLineType(parsed.LineTypeIntelligence?.Type);
        var normalized = !string.IsNullOrWhiteSpace(parsed.PhoneNumber)
            ? parsed.PhoneNumber
            : NormalizePhoneNumber(phoneNumberService, phoneNumber, parsed.CountryCode ?? countryCode);

        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalized,
            NationalFormat = parsed.NationalFormat,
            IsValid = parsed.Valid,
            IsReachable = parsed.Valid,
            IsMobile = lineType == PhoneNumberLineType.Mobile,
            IsLandline = lineType == PhoneNumberLineType.Landline,
            IsVoip = lineType == PhoneNumberLineType.Voip,
            LineType = lineType,
            CountryCode = parsed.CountryCode,
            CountryPrefix = NormalizeCountryPrefix(parsed.CallingCountryCode),
            Carrier = parsed.LineTypeIntelligence?.CarrierName,
            LineStatus = parsed.LineStatus?.Status,
            RiskScore = parsed.SmsPumpingRisk?.SmsPumpingRiskScore,
            RiskLevel = parsed.SmsPumpingRisk?.CarrierRiskCategory,
            IsAbuseDetected = parsed.SmsPumpingRisk?.NumberBlocked,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.Twilio,
            VerificationDateUtc = verificationDateUtc,
            RawProviderResponse = payload,
            Status = parsed.Valid
                ? PhoneNumberVerificationStatus.Verified
                : PhoneNumberVerificationStatus.Invalid,
        };

        if (!string.IsNullOrEmpty(normalized))
        {
            var timeZones = phoneNumberService.GetTimeZones(normalized);

            result.TimeZone = timeZones.Count > 0
                ? timeZones[0]
                : null;
        }

        AddMetadata(result, parsed);

        return result;
    }

    private PhoneNumberVerificationResult CreateFailedResult(string phoneNumber, string payload, string errorMessage)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.Twilio,
            VerificationDateUtc = _clock.UtcNow,
            RawProviderResponse = payload,
            Status = PhoneNumberVerificationStatus.Failed,
            LineType = PhoneNumberLineType.Unknown,
            ErrorMessage = errorMessage,
        };
    }

    private static string NormalizePhoneNumber(IPhoneNumberService phoneNumberService, string phoneNumber, string regionCode)
    {
        if (phoneNumberService.TryFormatToE164(phoneNumber, regionCode, out var e164Number))
        {
            return e164Number;
        }

        return phoneNumber;
    }

    private string ResolveCredentials(TwilioPhoneNumberVerificationSettings settings, IDataProtector protector)
    {
        var username = settings.AuthenticationType == TwilioPhoneNumberVerificationAuthenticationType.ApiKey
            ? settings.ApiKeySid
            : settings.AccountSid;
        var protectedPassword = settings.AuthenticationType == TwilioPhoneNumberVerificationAuthenticationType.ApiKey
            ? settings.ProtectedApiKeySecret
            : settings.ProtectedAuthToken;
        var password = Unprotect(protector, protectedPassword);

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    private string Unprotect(IDataProtector protector, string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt a Twilio Lookup secret. The value may have been encrypted with a different key.");

            return null;
        }
    }

    private static Uri BuildRequestUri(
        string endpoint,
        string phoneNumber,
        string countryCode,
        string fields)
    {
        var encodedPhoneNumber = Uri.EscapeDataString(phoneNumber);
        var requestEndpoint = endpoint.Contains("{PhoneNumber}", StringComparison.OrdinalIgnoreCase)
            ? endpoint.Replace("{PhoneNumber}", encodedPhoneNumber, StringComparison.OrdinalIgnoreCase)
            : endpoint.TrimEnd('/') + "/" + encodedPhoneNumber;
        var builder = new UriBuilder(requestEndpoint);
        var query = new StringBuilder(builder.Query.TrimStart('?'));

        AppendQueryParameter(query, "CountryCode", countryCode);
        AppendQueryParameter(query, "Fields", fields);

        builder.Query = query.ToString();

        return builder.Uri;
    }

    private static void AppendQueryParameter(StringBuilder query, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (query.Length > 0)
        {
            query.Append('&');
        }

        query.Append(name).Append('=').Append(Uri.EscapeDataString(value));
    }

    private static string NormalizeCountryPrefix(string countryPrefix)
    {
        if (string.IsNullOrWhiteSpace(countryPrefix))
        {
            return null;
        }

        var trimmed = countryPrefix.Trim();

        return trimmed.StartsWith('+')
            ? trimmed
            : "+" + trimmed;
    }

    private static PhoneNumberLineType MapLineType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return PhoneNumberLineType.Unknown;
        }

        return type.ToLower(CultureInfo.InvariantCulture) switch
        {
            "mobile" => PhoneNumberLineType.Mobile,
            "landline" or "fixedline" or "fixed-line" or "fixed_line" or "fixed line" => PhoneNumberLineType.Landline,
            "voip" or "fixedvoip" or "fixed-voip" or "fixed_voip" or "nonfixedvoip" or "non-fixed-voip" or "non_fixed_voip" => PhoneNumberLineType.Voip,
            "tollfree" or "toll-free" or "toll_free" => PhoneNumberLineType.TollFree,
            "premium" or "premiumrate" or "premium-rate" or "premium_rate" => PhoneNumberLineType.Premium,
            _ => PhoneNumberLineType.Unknown,
        };
    }

    private static void AddMetadata(PhoneNumberVerificationResult result, TwilioLookupResponse parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.Url))
        {
            result.Metadata["url"] = parsed.Url;
        }

        if (parsed.ValidationErrors is { Length: > 0 })
        {
            result.Metadata["validationErrors"] = string.Join(",", parsed.ValidationErrors);
        }

        if (!string.IsNullOrWhiteSpace(parsed.LineTypeIntelligence?.MobileCountryCode))
        {
            result.Metadata["mobileCountryCode"] = parsed.LineTypeIntelligence.MobileCountryCode;
        }

        if (!string.IsNullOrWhiteSpace(parsed.LineTypeIntelligence?.MobileNetworkCode))
        {
            result.Metadata["mobileNetworkCode"] = parsed.LineTypeIntelligence.MobileNetworkCode;
        }
    }
}
