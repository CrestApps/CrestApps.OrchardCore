using System.Net.Http.Json;
using System.Security.Cryptography;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Checks phone numbers against the Canada National Do Not Call List (DNCL).
/// Uses the LNNTE-DNCL API.
/// </summary>
/// <see href="https://www.lnnte-dncl.gc.ca/en/Organization/DNCL_API"/>
public sealed class CanadaDnclRegistry : INationalDoNotCallRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets the unique key identifying this registry.
    /// </summary>
    public string Key => "canada-lnnte-dncl";

    /// <summary>
    /// Gets the localized display name of this registry.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the localized description of this registry.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CanadaDnclRegistry"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service for reading settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="logger">The logger.</param>
    public CanadaDnclRegistry(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<CanadaDnclRegistry> stringLocalizer,
        ILogger<CanadaDnclRegistry> logger)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;

        DisplayName = stringLocalizer["Canada LNNTE-DNCL Registry"];
        Description = stringLocalizer["Checks phone numbers against the Canadian National Do Not Call List (LNNTE-DNCL) maintained by the CRTC."];
    }

    /// <inheritdoc/>
    public async Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumbers);

        var dncNumbers = new HashSet<PhoneNumber>();
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<CanadaDnclRegistrySettings>();

        if (string.IsNullOrWhiteSpace(settings?.ProtectedApiKey))
        {
            _logger.LogWarning("Canada DNCL Registry API key is not configured. Skipping registry check.");

            return dncNumbers;
        }

        string apiKey = null;

        var client = _httpClientFactory.CreateClient(nameof(CanadaDnclRegistry));
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://www.lnnte-dncl.gc.ca/api/"
            : settings.BaseUrl.TrimEnd('/') + "/";

        foreach (var phoneNumber in phoneNumbers)
        {
            if (!phoneNumber.HasValue)
            {
                continue;
            }

            var apiNumber = ConvertToApiFormat(phoneNumber.Value);

            if (apiNumber is null)
            {
                continue;
            }

            // The key is only read once a number this registry can answer for is actually being checked, so
            // a batch with no North American numbers never depends on the saved key being readable.
            apiKey ??= UnprotectApiKey(settings.ProtectedApiKey);

            try
            {
                var requestUrl = $"{baseUrl}DNCLNumbers/{apiNumber}?accountNumber={settings.AccountNumber}";

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("x-api-key", apiKey);

                var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // The registry did not say the number is unlisted; it did not say anything. Continuing
                    // here would turn that silence into a clean answer and let the number be called.
                    throw new DoNotCallScreeningException(
                        Key,
                        $"The Canada LNNTE-DNCL registry returned status {response.StatusCode}, so it could not report whether the number is listed.");
                }

                var result = await response.Content.ReadFromJsonAsync<DnclResponse>(cancellationToken);

                if (result?.IsRegistered == true)
                {
                    dncNumbers.Add(phoneNumber);
                }
            }
            catch (Exception ex) when (ex is not DoNotCallScreeningException && ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Error checking phone number against the Canada DNCL registry.");

                throw new DoNotCallScreeningException(
                    Key,
                    "The Canada LNNTE-DNCL registry could not be reached, so it could not report whether the number is listed.",
                    ex);
            }
        }

        return dncNumbers;
    }

    /// <summary>
    /// Decrypts the saved API key. A key that cannot be decrypted (saved under different data-protection
    /// keys, for example after the tenant's key ring was replaced) leaves the registry unable to answer. That
    /// is reported as a screening failure, the same as an unreachable registry, so callers treat the numbers
    /// as unscreened instead of crashing or treating them as cleared.
    /// </summary>
    /// <param name="protectedApiKey">The encrypted API key from the site settings.</param>
    /// <returns>The decrypted API key.</returns>
    private string UnprotectApiKey(string protectedApiKey)
    {
        try
        {
            var protector = _dataProtectionProvider.CreateProtector(DncRegistryConstants.DataProtectionPurposes.CanadaDnclApiKey);

            return protector.Unprotect(protectedApiKey);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(
                ex,
                "The saved API key for do-not-call registry {RegistryKey} could not be read, so the registry is unavailable for this check. Re-enter the API key in the Canada DNCL Registry settings.",
                Key);

            throw new DoNotCallScreeningException(
                Key,
                "The saved API key for the Canada LNNTE-DNCL registry could not be read, so it could not report whether the number is listed. Re-enter the API key in the registry settings.",
                ex);
        }
    }

    /// <summary>
    /// Converts an E.164 number to the 10-digit format expected by the Canada DNCL API, or returns
    /// <see langword="null"/> when the number is outside the North American numbering plan. The registry
    /// covers Canadian numbers only, so a number it cannot address is skipped rather than sent with its
    /// country code stripped, which would ask the registry about a different number entirely.
    /// </summary>
    /// <param name="e164Number">The canonical number.</param>
    /// <returns>The ten-digit national number, or <see langword="null"/> when the number is not addressable.</returns>
    private static string ConvertToApiFormat(string e164Number)
    {
        if (e164Number.StartsWith("+1", StringComparison.Ordinal) && e164Number.Length == 12)
        {
            return e164Number.Substring(2);
        }

        return null;
    }

    private sealed class DnclResponse
    {
        public bool IsRegistered { get; set; }
    }
}
