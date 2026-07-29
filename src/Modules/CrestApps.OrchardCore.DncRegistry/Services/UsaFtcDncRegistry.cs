using System.Net.Http.Json;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.PhoneNumbers;
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
    /// <param name="dataProtectionProvider">The data protection provider.</param>
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
    public async Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumbers);

        var dncNumbers = new HashSet<PhoneNumber>();
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
            if (!phoneNumber.HasValue)
            {
                continue;
            }

            try
            {
                var apiNumber = ConvertToApiFormat(phoneNumber.Value);

                if (apiNumber is null)
                {
                    continue;
                }
                var requestUrl = $"{baseUrl}Check?PhoneNumber={apiNumber}&OrganizationId={settings.OrganizationId}&api_key={apiKey}";

                var response = await client.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // The registry did not say the number is unlisted; it did not say anything. Continuing
                    // here would turn that silence into a clean answer and let the number be called.
                    throw new DoNotCallScreeningException(
                        Key,
                        $"The United States FTC do-not-call registry returned status {response.StatusCode}, so it could not report whether the number is listed.");
                }

                var result = await response.Content.ReadFromJsonAsync<FtcDncResponse>(cancellationToken);

                if (result?.IsOnDnc == true)
                {
                    dncNumbers.Add(phoneNumber);
                }
            }
            catch (Exception ex) when (ex is not DoNotCallScreeningException && ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Error checking phone number against the FTC DNC registry.");

                throw new DoNotCallScreeningException(
                    Key,
                    "The United States FTC do-not-call registry could not be reached, so it could not report whether the number is listed.",
                    ex);
            }
        }

        return dncNumbers;
    }

    /// <summary>
    /// Converts an E.164 number to the 10-digit format expected by the FTC API, or returns
    /// <see langword="null"/> when the number is outside the North American numbering plan. The registry
    /// covers United States numbers only, so a number it cannot address is skipped rather than sent with its
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

    private sealed class FtcDncResponse
    {
        public bool IsOnDnc { get; set; }
    }
}
