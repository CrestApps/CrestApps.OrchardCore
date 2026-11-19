using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Turns on the My Account feature on a tenant that ran Commerce before the account area moved to Users: Orchard doesn't
/// add a feature's new dependency to an enabled feature's shell descriptor, so that tenant would serve the area without
/// the feature being on. It checks when the tenant starts (Commerce owns no migrations) and does nothing once the feature
/// is on, which a new tenant gets as a dependency anyway.
/// </summary>
internal sealed class CommerceAccountFeatureUpgrade : ModularTenantEvents
{
    private const string CommerceFeatureId = "CrestApps.OrchardCore.Commerce";

    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public CommerceAccountFeatureUpgrade(ShellSettings shellSettings, ILogger<CommerceAccountFeatureUpgrade> logger)
    {
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public override Task ActivatedAsync()
    {
        if (_shellSettings.IsInitializing())
        {
            return Task.CompletedTask;
        }

        // Deferred until activation has finished: enabling a feature reloads the tenant.
        ShellScope.AddDeferredTask(async scope =>
        {
            try
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
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "My Account couldn't be turned on for this Commerce tenant; turn it on in Features.");
            }
        });

        return Task.CompletedTask;
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
}
