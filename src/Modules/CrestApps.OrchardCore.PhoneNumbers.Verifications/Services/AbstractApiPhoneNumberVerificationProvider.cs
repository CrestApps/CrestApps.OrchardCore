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
/// Verifies phone numbers using the AbstractAPI Phone Validation service and maps the
/// native response into the provider-agnostic <see cref="PhoneNumberVerificationResult"/>.
/// </summary>
public sealed class AbstractApiPhoneNumberVerificationProvider : IPhoneNumberVerificationProvider
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.AbstractApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractApiPhoneNumberVerificationProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service used to read provider settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to decrypt secrets.</param>
    /// <param name="phoneNumberService">The phone number service used to resolve time zones.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AbstractApiPhoneNumberVerificationProvider(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<AbstractApiPhoneNumberVerificationProvider> logger)
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

        var settings = await _siteService.GetSettingsAsync<AbstractApiPhoneNumberVerificationSettings>();
        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var apiKey = Unprotect(protector, settings.ProtectedApiKey);

        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? "https://phonevalidation.abstractapi.com/v1/"
            : settings.Endpoint;

        var requestUri = BuildRequestUri(endpoint, apiKey, phoneNumber);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        ApplyAuthentication(request, settings, protector);

        var client = _httpClientFactory.CreateClient(nameof(AbstractApiPhoneNumberVerificationProvider));

        using var response = await client.SendAsync(request, cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AbstractAPI returned status code {StatusCode} while verifying a phone number.", (int)response.StatusCode);

            return CreateFailedResult(phoneNumber, payload);
        }

        return MapResponse(phoneNumber, payload);
    }

    private PhoneNumberVerificationResult MapResponse(string phoneNumber, string payload)
    {
        AbstractApiResponse parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<AbstractApiResponse>(
                payload,
                PhoneNumberVerificationProviderJsonSerializerOptions.Default);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse the AbstractAPI phone validation response.");

            return CreateFailedResult(phoneNumber, payload);
        }

        if (parsed is null)
        {
            return CreateFailedResult(phoneNumber, payload);
        }

        var lineType = MapLineType(parsed.Type);
        var normalized = !string.IsNullOrWhiteSpace(parsed.InternationalFormat)
            ? parsed.InternationalFormat
            : NormalizePhoneNumber(phoneNumber, parsed.Country?.Code);

        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalized,
            NationalFormat = parsed.LocalFormat,
            IsValid = parsed.Valid,
            IsReachable = parsed.Valid,
            IsMobile = lineType == PhoneNumberLineType.Mobile,
            IsLandline = lineType == PhoneNumberLineType.Landline,
            IsVoip = lineType == PhoneNumberLineType.Voip,
            LineType = lineType,
            CountryCode = parsed.Country?.Code,
            CountryName = parsed.Country?.Name,
            Carrier = parsed.Carrier,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.AbstractApi,
            VerificationDateUtc = _clock.UtcNow,
            RawProviderResponse = payload,
            Status = parsed.Valid ? PhoneNumberVerificationStatus.Verified : PhoneNumberVerificationStatus.Invalid,
        };

        if (!string.IsNullOrEmpty(normalized))
        {
            var timeZones = _phoneNumberService.GetTimeZones(normalized);

            result.TimeZone = timeZones.Count > 0
                ? timeZones[0]
                : null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.Location))
        {
            result.Metadata["location"] = parsed.Location;
        }

        return result;
    }

    private PhoneNumberVerificationResult CreateFailedResult(string phoneNumber, string payload)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.AbstractApi,
            VerificationDateUtc = _clock.UtcNow,
            RawProviderResponse = payload,
            Status = PhoneNumberVerificationStatus.Failed,
            LineType = PhoneNumberLineType.Unknown,
        };
    }

    private string NormalizePhoneNumber(string phoneNumber, string regionCode)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, regionCode, out var e164Number))
        {
            return e164Number;
        }

        return phoneNumber;
    }

    private static Uri BuildRequestUri(string endpoint, string apiKey, string phoneNumber)
    {
        var builder = new UriBuilder(endpoint);
        var query = new StringBuilder(builder.Query.TrimStart('?'));

        if (query.Length > 0)
        {
            query.Append('&');
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            query.Append("api_key=").Append(Uri.EscapeDataString(apiKey)).Append('&');
        }

        query.Append("phone=").Append(Uri.EscapeDataString(phoneNumber));
        builder.Query = query.ToString();

        return builder.Uri;
    }

    private void ApplyAuthentication(
        HttpRequestMessage request,
        AbstractApiPhoneNumberVerificationSettings settings,
        IDataProtector protector)
    {
        if (settings.AuthenticationType == PhoneNumberVerificationAuthenticationType.Basic
            && !string.IsNullOrWhiteSpace(settings.Username))
        {
            var password = Unprotect(protector, settings.ProtectedPassword);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{password}"));

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
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
            _logger.LogError(ex, "Failed to decrypt an AbstractAPI secret. The value may have been encrypted with a different key.");

            return null;
        }
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
            "landline" => PhoneNumberLineType.Landline,
            "voip" => PhoneNumberLineType.Voip,
            "toll_free" or "tollfree" => PhoneNumberLineType.TollFree,
            "premium" or "premium_rate" => PhoneNumberLineType.Premium,
            _ => PhoneNumberLineType.Unknown,
        };
    }
}
