using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Records the tenant hierarchy events of a parent tenant in the Orchard Core audit trail, under the
/// <see cref="HierarchyAuditEventNames.Category"/> category. The child tenant's registry entry is the correlation
/// identifier, so the audit trail can show the events of one child tenant.
/// </summary>
public sealed class HierarchyAuditLog
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyAuditLog"/> class.
    /// </summary>
    /// <param name="serviceProvider">The tenant services, to resolve the audit trail when it is enabled.</param>
    /// <param name="logger">The logger.</param>
    public HierarchyAuditLog(
        IServiceProvider serviceProvider,
        ILogger<HierarchyAuditLog> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Records an event. Does nothing, apart from a warning, when the audit trail is not enabled in the tenant.
    /// </summary>
    /// <param name="auditEvent">The event.</param>
    public async Task RecordAsync(HierarchyAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var auditTrailManager = _serviceProvider.GetService<IAuditTrailManager>();

        if (auditTrailManager is null)
        {
            _logger.LogWarning("The tenant hierarchy event '{Event}' was not recorded because the Audit Trail feature is not enabled.", auditEvent.Name);

            return;
        }

        await auditTrailManager.RecordEventAsync(new AuditTrailContext<HierarchyAuditEvent>(
            auditEvent.Name,
            HierarchyAuditEventNames.Category,
            auditEvent.ChildEntryId,
            auditEvent.UserId,
            auditEvent.UserName,
            auditEvent));
    }
}
