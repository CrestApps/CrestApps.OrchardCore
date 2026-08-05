using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the inbound entry-point administration screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
public sealed class ContactCenterEntryPointsAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ContactCenterEntryPoint, ContactCenterEntryPointDisplayDriver>();
        services.AddNavigationProvider<ContactCenterEntryPointsAdminMenu>();
    }
}
