using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

/// <summary>
/// Enables the AI Usage Analytics feature on sites that had the retired <c>EnableAIUsageTracking</c> site setting
/// ticked, so they keep recording AI usage now that the feature is what turns it on.
/// </summary>
internal sealed class AIUsageAnalyticsFeatureMigrations : DataMigration
{
    private const string GeneralAISettingsName = "GeneralAISettings";
    private const string EnableAIUsageTrackingName = "EnableAIUsageTracking";

    private readonly ShellSettings _shellSettings;
    private readonly ISiteService _siteService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIUsageAnalyticsFeatureMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="siteService">The site service, for the retired setting.</param>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    public AIUsageAnalyticsFeatureMigrations(
        ShellSettings shellSettings,
        ISiteService siteService,
        IShellFeaturesManager shellFeaturesManager)
    {
        _shellSettings = shellSettings;
        _siteService = siteService;
        _shellFeaturesManager = shellFeaturesManager;
    }

    /// <summary>
    /// Enables the AI Usage Analytics feature when the retired setting was ticked.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        if (_shellSettings.IsInitializing())
        {
            return 1;
        }

        // The setting is read from the stored JSON so this keeps working once the property leaves GeneralAISettings.
        var site = await _siteService.GetSiteSettingsAsync();
        var trackingEnabled = site.Properties[GeneralAISettingsName]?[EnableAIUsageTrackingName]?.GetValue<bool>() == true;

        if (!trackingEnabled || await _shellFeaturesManager.IsFeatureEnabledAsync(AIConstants.Feature.UsageAnalytics))
        {
            return 1;
        }

        // Migrations that run while a tenant starts run no deferred tasks, so the feature is enabled after the request
        // ends instead. Returning 0 leaves the migration unrecorded, so it runs again at the next start until the
        // feature is on.
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("enable-ai-usage-analytics", static async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await shellFeaturesManager.IsFeatureEnabledAsync(AIConstants.Feature.UsageAnalytics))
            {
                return;
            }

            var feature = (await shellFeaturesManager.GetAvailableFeaturesAsync())
                .FirstOrDefault(feature => feature.Id == AIConstants.Feature.UsageAnalytics);

            if (feature is not null)
            {
                // Without force, the feature is refused when any feature in its dependency chain is still off.
                await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
            }
        });

        return 0;
    }
}
