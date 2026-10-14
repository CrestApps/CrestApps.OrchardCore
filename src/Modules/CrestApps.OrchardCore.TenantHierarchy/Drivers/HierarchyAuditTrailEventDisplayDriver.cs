using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using OrchardCore;
using OrchardCore.AuditTrail.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Shows the tenant hierarchy data of an audit trail event: the child tenant, why a session ended, how the user
/// signed in, the session and the address the request came from.
/// </summary>
public sealed class HierarchyAuditTrailEventDisplayDriver : DisplayDriver<AuditTrailEvent>
{
    private readonly HierarchyLabelsProvider _labelsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyAuditTrailEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="labelsProvider">The words the parent uses for its child tenants.</param>
    public HierarchyAuditTrailEventDisplayDriver(HierarchyLabelsProvider labelsProvider)
    {
        _labelsProvider = labelsProvider;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(AuditTrailEvent auditTrailEvent, BuildDisplayContext context)
    {
        if (auditTrailEvent.Category != HierarchyAuditEventNames.Category || !auditTrailEvent.TryGet<HierarchyAuditEvent>(out var data))
        {
            return null;
        }

        var labels = _labelsProvider.GetLabels();

        void Build(HierarchyAuditEventViewModel model)
        {
            model.AuditTrailEvent = auditTrailEvent;
            model.Data = data;
            model.Labels = labels;
        }

        return Combine(
            Initialize<HierarchyAuditEventViewModel>("HierarchyAuditEventData_SummaryAdmin", Build)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "EventData:10"),
            Initialize<HierarchyAuditEventViewModel>("HierarchyAuditEventDetail_DetailAdmin", Build)
                .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:10"));
    }
}
