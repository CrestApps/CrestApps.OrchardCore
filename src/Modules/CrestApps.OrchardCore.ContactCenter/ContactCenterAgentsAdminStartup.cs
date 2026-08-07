using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the agent administration screens.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class ContactCenterAgentsAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AgentStateReasonCode, AgentStateReasonCodeDisplayDriver>();
        services.AddNavigationProvider<ContactCenterAgentsAdminMenu>();
    }
}
