using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// OrchardCore-specific AIDeploymentManager that reads global default deployment
/// settings from ISiteService instead of IOptions.
/// </summary>
public sealed class SiteSettingsAIDeploymentManager : AIDeploymentManagerBase
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteSettingsAIDeploymentManager"/> class.
    /// </summary>
    /// <param name="deploymentStore">The deployment store for persistence.</param>
    /// <param name="handlers">The catalog entry handlers for deployment lifecycle events.</param>
    /// <param name="siteService">The site service for reading global deployment settings.</param>
    /// <param name="logger">The logger instance.</param>
    public SiteSettingsAIDeploymentManager(
        IAIDeploymentStore deploymentStore,
        IEnumerable<ICatalogEntryHandler<AIDeployment>> handlers,
        ISiteService siteService,
        ILogger<SiteSettingsAIDeploymentManager> logger)
        : base(deploymentStore, handlers, logger)
    {
        _siteService = siteService;
    }

    private DefaultAIDeploymentSettings _cachedSettings;

    protected override async ValueTask<DefaultAIDeploymentSettings> GetDefaultAIDeploymentSettingsAsync()
    {
        _cachedSettings ??= await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();

        return _cachedSettings;
    }
}
