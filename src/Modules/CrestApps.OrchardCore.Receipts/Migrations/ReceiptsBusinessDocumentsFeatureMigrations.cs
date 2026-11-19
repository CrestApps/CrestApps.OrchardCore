using CrestApps.OrchardCore.BusinessDocuments;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Receipts.Migrations;

/// <summary>
/// Enables the Business Documents feature on a site that had Receipts on before receipts were printed as business
/// documents.
/// </summary>
/// <remarks>
/// Receipts depends on Business Documents, so Orchard Core loads it, but a feature that only became a dependency is not
/// recorded as enabled. Recording it keeps the document templates and the number series page on however the site's
/// features change later, and makes the feature list show what the site runs.
/// </remarks>
internal sealed class ReceiptsBusinessDocumentsFeatureMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public ReceiptsBusinessDocumentsFeatureMigrations(ShellSettings shellSettings, IShellFeaturesManager shellFeaturesManager)
    {
        _shellSettings = shellSettings;
        _shellFeaturesManager = shellFeaturesManager;
    }

    public async Task<int> CreateAsync()
    {
        if (!await NeedsEnablingAsync(_shellSettings, _shellFeaturesManager))
        {
            return 1;
        }

        // Migrations that run while a tenant starts run no deferred tasks, so the feature is enabled after the request
        // ends instead. Returning 0 leaves the migration unrecorded, so it runs again at the next start until the
        // feature is on.
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("enable-business-documents", static async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await shellFeaturesManager.IsFeatureEnabledAsync(BusinessDocumentsConstants.Features.Area))
            {
                return;
            }

            var feature = (await shellFeaturesManager.GetAvailableFeaturesAsync())
                .FirstOrDefault(feature => feature.Id == BusinessDocumentsConstants.Features.Area);

            if (feature is not null)
            {
                // Without force, the feature is refused when any feature in its dependency chain is still off.
                await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
            }
        });

        return 0;
    }

    // A new tenant enables the whole chain while it is set up; only an existing one can miss the new dependency.
    internal static async Task<bool> NeedsEnablingAsync(ShellSettings shellSettings, IShellFeaturesManager shellFeaturesManager)
        => !shellSettings.IsInitializing() &&
            !await shellFeaturesManager.IsFeatureEnabledAsync(BusinessDocumentsConstants.Features.Area);
}
