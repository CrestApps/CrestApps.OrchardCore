using CrestApps.OrchardCore.Dialpad.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Resolves the active Dialpad environment and its protected secrets when the tenant options are first loaded.
/// </summary>
internal sealed class DialpadOptionsConfigurations : IConfigureOptions<DialpadOptions>
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadOptionsConfigurations"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read Dialpad settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect secrets.</param>
    /// <param name="logger">The logger.</param>
    public DialpadOptionsConfigurations(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DialpadOptionsConfigurations> logger)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Configure(DialpadOptions options)
    {
        var settings = _siteService.GetSettings<DialpadSettings>();
        var environment = settings.GetActiveEnvironmentSettings();
        var apiTokenProtector = _dataProtectionProvider.CreateProtector(DialpadConstants.ProtectorName);
        var clientSecretProtector = _dataProtectionProvider.CreateProtector(DialpadConstants.OAuthProtectorName);

        options.IsEnabled = settings.IsEnabled;
        options.Environment = settings.Environment;
        options.Host = environment.Host;
        options.ApiBaseUrl = environment.ApiBaseUrl;
        options.UserId = environment.UserId;
        options.OutboundCallerId = environment.OutboundCallerId;
        options.ApiToken = string.IsNullOrEmpty(environment.ApiToken) ? null : Unprotect(apiTokenProtector, environment.ApiToken);
        options.AuthenticationType = environment.AuthenticationType;
        options.ClientId = environment.ClientId;
        options.ClientSecret = string.IsNullOrEmpty(environment.ClientSecret) ? null : Unprotect(clientSecretProtector, environment.ClientSecret);
        options.Scopes = environment.Scopes;
    }

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to unprotect a Dialpad secret.");

            return null;
        }
    }
}
