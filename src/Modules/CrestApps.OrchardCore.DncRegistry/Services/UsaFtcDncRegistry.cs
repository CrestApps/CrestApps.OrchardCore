using System.Net.Http.Json;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Checks phone numbers against the USA FTC Do Not Call (DNC) registry.
/// Uses the telemarketing.donotcall.gov API.
/// </summary>
/// <see href="https://telemarketing.donotcall.gov"/>
public sealed class UsaFtcDncRegistry : INationalDoNotCallRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Gets the unique key identifying this registry.
    /// </summary>
    public string Key => "usa-ftc-dnc";

    /// <summary>
    /// Gets the localized display name of this registry.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the localized description of this registry.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UsaFtcDncRegistry"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service for reading settings.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="logger">The logger.</param>
    public UsaFtcDncRegistry(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<UsaFtcDncRegistry> stringLocalizer,
        ILogger<UsaFtcDncRegistry> logger)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;

        DisplayName = stringLocalizer["USA FTC Do Not Call Registry"];
        Description = stringLocalizer["Checks phone numbers against the United States Federal Trade Commission (FTC) National Do Not Call Registry."];
    }

    /// <inheritdoc/>
    public async Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        var dncNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<UsaFtcDncRegistrySettings>();

        if (string.IsNullOrWhiteSpace(settings?.ProtectedApiKey))
        {
            _logger.LogWarning("USA FTC DNC Registry API key is not configured. Skipping registry check.");

            return dncNumbers;
        }

        var protector = _dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.DncRegistry.UsaFtcSettings");
        var apiKey = protector.Unprotect(settings.ProtectedApiKey);

        var client = _httpClientFactory.CreateClient(nameof(UsaFtcDncRegistry));
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://telemarketing.donotcall.gov/api/"
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
                var requestUrl = $"{baseUrl}Check?PhoneNumber={normalizedNumber}&OrganizationId={settings.OrganizationId}&api_key={apiKey}";

                var response = await client.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "FTC DNC API returned status {StatusCode} for phone number lookup.",
                        response.StatusCode);

                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<FtcDncResponse>(cancellationToken);

                if (result?.IsOnDnc == true)
                {
                    dncNumbers.Add(phoneNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking phone number against FTC DNC registry.");
            }
        }

        return dncNumbers;
    }

    private static string NormalizePhoneNumber(string phoneNumber)
        => new(phoneNumber.Where(char.IsDigit).ToArray());

    private sealed class FtcDncResponse
    {
        public bool IsOnDnc { get; set; }
    }
}
