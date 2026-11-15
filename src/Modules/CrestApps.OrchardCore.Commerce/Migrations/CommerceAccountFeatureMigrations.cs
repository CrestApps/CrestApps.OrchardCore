using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Commerce.Migrations;

/// <summary>
/// Turns on the My Account feature on a tenant that ran Commerce before the account area moved to Users: Orchard doesn't
/// add a feature's new dependency to an enabled feature's shell descriptor, so that tenant would serve the area without
/// the feature being on. A new tenant gets it as a dependency and needs nothing.
/// </summary>
internal sealed class CommerceAccountFeatureMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public CommerceAccountFeatureMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 1;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var features = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabled = (await features.GetEnabledFeaturesAsync()).Select(feature => feature.Id);

            if (!NeedsAccount(enabled))
            {
                return;
            }

            var account = scope.ServiceProvider.GetRequiredService<IExtensionManager>().GetFeatures((IEnumerable<string>)[UsersConstants.Feature.Account]).FirstOrDefault();

            if (account is not null)
            {
                await features.EnableFeaturesAsync([account], force: true);
            }
        });

        return 1;
    }

    /// <summary>
    /// Whether a tenant with these features enabled needs My Account turned on.
    /// </summary>
    /// <param name="enabledFeatureIds">The tenant's enabled features.</param>
    /// <returns>Whether it does.</returns>
    internal static bool NeedsAccount(IEnumerable<string> enabledFeatureIds)
    {
        var enabled = enabledFeatureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return enabled.Contains(CommerceFeatureId) && !enabled.Contains(UsersConstants.Feature.Account);
    }

    private const string CommerceFeatureId = "CrestApps.OrchardCore.Commerce";
}
