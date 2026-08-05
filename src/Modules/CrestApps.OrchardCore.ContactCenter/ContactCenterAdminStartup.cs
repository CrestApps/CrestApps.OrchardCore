using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center settings screens and administration menu.
/// </summary>
/// <remarks>
/// Each capability owns its administration screens so enabling a capability provides a complete management
/// experience without requiring another feature toggle.
/// </remarks>
[Feature(ContactCenterConstants.Feature.Area)]
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
