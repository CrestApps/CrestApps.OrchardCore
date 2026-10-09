using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.AuditTrail.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.ViewModels;

/// <summary>
/// The view model of the tenant hierarchy data of an audit trail event.
/// </summary>
public class HierarchyAuditEventViewModel
{
    /// <summary>
    /// Gets or sets the audit trail event.
    /// </summary>
    public AuditTrailEvent AuditTrailEvent { get; set; }

    /// <summary>
    /// Gets or sets the tenant hierarchy data of the event.
    /// </summary>
    public HierarchyAuditEvent Data { get; set; }

    /// <summary>
    /// Gets or sets the words the parent uses for its child tenants.
    /// </summary>
    public HierarchyLabels Labels { get; set; }
}
