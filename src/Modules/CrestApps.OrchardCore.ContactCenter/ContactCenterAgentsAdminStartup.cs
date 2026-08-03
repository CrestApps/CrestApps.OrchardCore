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
/// <remarks>
/// The agent capability itself - profiles, presence, capacity, skills and queue sign-in - is usable without any
/// screens, so the screens are a separate feature. A deployment that drives agents through its own front end or
/// an API can enable the capability and leave this off.
/// </remarks>
[Feature(ContactCenterConstants.Feature.Admin)]
[RequireFeatures(ContactCenterConstants.Feature.Agents)]
public sealed class ContactCenterAgentsAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AgentStateReasonCode, AgentStateReasonCodeDisplayDriver>();
        services.AddNavigationProvider<ContactCenterAgentsAdminMenu>();
    }
}
