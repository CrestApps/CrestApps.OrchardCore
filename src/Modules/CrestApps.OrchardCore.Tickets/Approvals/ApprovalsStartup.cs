using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// Registers ticket approvals: the steps on the ticket type, the wait when a ticket is created, the ticket's Approval
/// panel and the approval page.
/// </summary>
[Feature(TicketsConstants.Features.Approvals)]
public sealed class ApprovalsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketApprovalStarter>());
        services.AddPermissionProvider<TicketApprovalsPermissionProvider>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TicketApprovalSettingsDisplayDriver>();
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketApprovalDisplayDriver>();
    }
}

/// <summary>
/// Tells approvers through Orchard's notifications when a ticket waits for them.
/// </summary>
[Feature(TicketsConstants.Features.Approvals)]
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class ApprovalNotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketApprovalNotifier>());
}

/// <summary>
/// Finds a requester's manager from their staff record, for steps the requester's manager decides.
/// </summary>
[Feature(TicketsConstants.Features.Approvals)]
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class ApprovalStaffStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketRequesterManagerSource, StaffRequesterManagerSource>());
}
