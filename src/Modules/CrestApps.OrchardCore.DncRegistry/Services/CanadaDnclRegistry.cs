using System.Net.Http.Json;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
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
    public async Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        var dncNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<CanadaDnclRegistrySettings>();

        if (string.IsNullOrWhiteSpace(settings?.ProtectedApiKey))
        {
            _logger.LogWarning("Canada DNCL Registry API key is not configured. Skipping registry check.");

            return dncNumbers;
        }

        var protector = _dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.DncRegistry.CanadaDnclSettings");
        var apiKey = protector.Unprotect(settings.ProtectedApiKey);

        var client = _httpClientFactory.CreateClient(nameof(CanadaDnclRegistry));
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://www.lnnte-dncl.gc.ca/api/"
            : settings.BaseUrl.TrimEnd('/') + "/";

        foreach (var phoneNumber in phoneNumbers)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                continue;
            }

            try
            {
                var normalizedNumber = NormalizePhoneNumber(phoneNumber);
                var requestUrl = $"{baseUrl}DNCLNumbers/{normalizedNumber}?accountNumber={settings.AccountNumber}";

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("x-api-key", apiKey);

                var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Canada DNCL API returned status {StatusCode} for phone number lookup.",
                        response.StatusCode);

                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<DnclResponse>(cancellationToken);

                if (result?.IsRegistered == true)
                {
                    dncNumbers.Add(phoneNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking phone number against Canada DNCL registry.");
            }
        }

        return dncNumbers;
    }

    private static string NormalizePhoneNumber(string phoneNumber)
        => new string(phoneNumber.Where(char.IsDigit).ToArray());

    private sealed class DnclResponse
    {
        public bool IsRegistered { get; set; }
    }
}
