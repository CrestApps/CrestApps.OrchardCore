using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center settings screens and the administration menu every capability screen hangs off.
/// </summary>
/// <remarks>
/// This is the root of the administration surface rather than a marker. Each capability's screens live in its own
/// <c>.Admin</c> feature, and every one of them depends on this feature for the menu they attach to, so enabling a
/// capability alone leaves a deployment headless.
/// </remarks>
[Feature(ContactCenterConstants.Feature.Admin)]
public sealed class ContactCenterAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddResourceConfiguration<ContactCenterExternalTransferResourceConfiguration>()
            .AddSiteDisplayDriver<ContactCenterExternalTransferSettingsDisplayDriver>()
            .AddNavigationProvider<ContactCenterSettingsAdminMenu>();
    }
}
