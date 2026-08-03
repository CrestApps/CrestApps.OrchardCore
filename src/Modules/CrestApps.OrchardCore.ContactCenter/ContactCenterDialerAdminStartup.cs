using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the outbound dialer administration screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Admin)]
[RequireFeatures(ContactCenterConstants.Feature.Dialer)]
public sealed class ContactCenterDialerAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<DialerProfile, DialerProfileDisplayDriver>();
        services.AddNavigationProvider<ContactCenterDialerAdminMenu>();
    }
}
