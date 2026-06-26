using System.Globalization;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Verifies phone numbers using Veriphone and maps the native response into the
/// provider-agnostic <see cref="PhoneNumberVerificationResult"/>.
/// </summary>
public sealed class VeriphonePhoneNumberVerificationProvider : IPhoneNumberVerificationProvider
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Veriphone";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VeriphonePhoneNumberVerificationProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service used to read provider settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to decrypt secrets.</param>
    /// <param name="phoneNumberService">The phone number service used to resolve time zones.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public VeriphonePhoneNumberVerificationProvider(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<VeriphonePhoneNumberVerificationProvider> logger)
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

        var settings = await _siteService.GetSettingsAsync<VeriphonePhoneNumberVerificationSettings>();
        var protector = _dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var apiKey = Unprotect(protector, settings.ProtectedApiKey);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Veriphone API key is not configured. Skipping phone number verification.");

            return CreateFailedResult(phoneNumber, null);
        }

        var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? VeriphonePhoneNumberVerificationSettings.DefaultEndpoint
            : settings.Endpoint;

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(endpoint, phoneNumber));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var client = _httpClientFactory.CreateClient(nameof(VeriphonePhoneNumberVerificationProvider));

        using var response = await client.SendAsync(request, cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Veriphone returned status code {StatusCode} while verifying a phone number.", (int)response.StatusCode);

            return CreateFailedResult(phoneNumber, payload);
        }

        return MapResponse(phoneNumber, payload);
    }

    private PhoneNumberVerificationResult MapResponse(string phoneNumber, string payload)
    {
        VeriphoneResponse parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<VeriphoneResponse>(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse the Veriphone phone validation response.");

            return CreateFailedResult(phoneNumber, payload);
        }

        if (parsed is null ||
            !string.Equals(parsed.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailedResult(phoneNumber, payload);
        }

        var lineType = MapLineType(parsed.PhoneType);
        var normalized = !string.IsNullOrWhiteSpace(parsed.E164)
            ? parsed.E164
            : NormalizePhoneNumber(phoneNumber, parsed.CountryCode);

        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalized,
            NationalFormat = parsed.LocalNumber,
            IsValid = parsed.PhoneValid,
            IsReachable = parsed.PhoneValid,
            IsMobile = lineType == PhoneNumberLineType.Mobile,
            IsLandline = lineType == PhoneNumberLineType.Landline,
            IsVoip = lineType == PhoneNumberLineType.Voip,
            LineType = lineType,
            CountryCode = parsed.CountryCode,
            CountryName = parsed.Country,
            CountryPrefix = NormalizeCountryPrefix(parsed.CountryPrefix),
            Region = parsed.PhoneRegion,
            Carrier = parsed.Carrier,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.Veriphone,
            VerificationDateUtc = _clock.UtcNow,
            RawProviderResponse = payload,
            Status = parsed.PhoneValid
                ? PhoneNumberVerificationStatus.Verified
                : PhoneNumberVerificationStatus.Invalid,
        };

        if (!string.IsNullOrEmpty(normalized))
        {
            var timeZones = _phoneNumberService.GetTimeZones(normalized);

            result.TimeZone = timeZones.Count > 0
                ? timeZones[0]
                : null;
        }

        if (!string.IsNullOrWhiteSpace(parsed.InternationalNumber))
        {
            result.Metadata["internationalNumber"] = parsed.InternationalNumber;
        }

        return result;
    }

    private PhoneNumberVerificationResult CreateFailedResult(string phoneNumber, string payload)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            VerificationProvider = PhoneNumberVerificationsConstants.Providers.Veriphone,
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
            _logger.LogError(ex, "Failed to decrypt a Veriphone secret. The value may have been encrypted with a different key.");

            return null;
        }
    }

    private static Uri BuildRequestUri(string endpoint, string phoneNumber)
    {
        var builder = new UriBuilder(endpoint);
        var query = new StringBuilder(builder.Query.TrimStart('?'));

        if (query.Length > 0)
        {
            query.Append('&');
        }

        query.Append("phone=").Append(Uri.EscapeDataString(phoneNumber));
        builder.Query = query.ToString();

        return builder.Uri;
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
            "fixed-line" or "fixed_line" or "fixed line" or "landline" => PhoneNumberLineType.Landline,
            "voip" => PhoneNumberLineType.Voip,
            "toll-free" or "toll_free" or "tollfree" => PhoneNumberLineType.TollFree,
            "premium" or "premium-rate" or "premium_rate" => PhoneNumberLineType.Premium,
            _ => PhoneNumberLineType.Unknown,
        };
    }
}
