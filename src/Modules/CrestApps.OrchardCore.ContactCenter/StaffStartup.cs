using CrestApps.OrchardCore.ContactCenter.Staff;
using CrestApps.OrchardCore.Staff;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Makes Contact Center agents staff members: the Agent capability, and signing out people who leave.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <summary>
    /// The staff capability of Contact Center agents.
    /// </summary>
    public const string AgentCapability = "Agent";

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffCapability(AgentCapability, LocalizationSource.Create<StaffStartup>("Contact Center agent"), LocalizationSource.Create<StaffStartup>("Handles calls and messages. Signing in to a queue makes a person an agent."), 10);
        services.AddStaffLifecycleObserver<ContactCenterStaffLifecycleObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AgentProfile>, AgentProfileStaffHandler>());
        services.AddDisplayDriver<StaffMember, AgentStaffDisplayDriver>();
    }
}
