using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.TaxJar;

/// <summary>
/// The TaxJar account sales tax is worked out with, under Commerce › Taxation › TaxJar.
/// </summary>
public sealed class TaxJarSettings
{
    public const string ProtectionPurpose = "CrestApps.OrchardCore.Taxation.TaxJar.ApiKey";

    /// <summary>
    /// Gets or sets the protected API token.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether TaxJar's sandbox answers, for a sandbox token.
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Gets or sets the two-letter codes of the countries TaxJar works tax out for; orders to the others are taxed by
    /// the store's own rules.
    /// </summary>
    public List<string> Countries { get; set; } = ["US"];
}

/// <summary>
/// The TaxJar settings as the provider reads them: the token unprotected. Read through
/// <see cref="IOptionsMonitor{TOptions}.CurrentValue"/>; saving the settings refreshes them without restarting the tenant.
/// </summary>
public sealed class TaxJarOptions
{
    public string ApiKey { get; set; }

    public bool UseSandbox { get; set; }

    public IReadOnlyList<string> Countries { get; set; } = [];

    public string BaseUrl
        => UseSandbox ? "https://api.sandbox.taxjar.com/v2/" : "https://api.taxjar.com/v2/";
}

internal sealed class TaxJarOptionsConfiguration : IConfigureOptions<TaxJarOptions>
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    public TaxJarOptionsConfiguration(ISiteService siteService, IDataProtectionProvider dataProtectionProvider, ILogger<TaxJarOptionsConfiguration> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public void Configure(TaxJarOptions options)
    {
        var settings = _siteService.GetSettings<TaxJarSettings>();

        options.UseSandbox = settings.UseSandbox;
        options.Countries = [.. (settings.Countries ?? []).Where(code => code?.Length == 2).Select(code => code.ToUpperInvariant())];
        options.ApiKey = _dataProtectionProvider.UnprotectSettingOrDefault(TaxJarSettings.ProtectionPurpose, settings.ApiKey, _logger, "TaxJar token");
    }
}
