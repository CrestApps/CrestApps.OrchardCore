using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AuditTrail.Indexes;
using OrchardCore.AuditTrail.Models;
using OrchardCore.Entities;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Reads the tenant hierarchy events a parent recorded in its Orchard Core audit trail.
/// </summary>
internal static class AuditTrailHelper
{
    /// <summary>
    /// Returns the tenant hierarchy events about one child tenant, newest first.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="parent">The parent tenant name.</param>
    /// <param name="childEntryId">The registry entry of the child tenant, the correlation identifier of its events.</param>
    public static Task<List<(AuditTrailEvent Event, HierarchyAuditEvent Data)>> GetEventsAsync(TenantHierarchyTestHost host, string parent, string childEntryId)
    {
        return host.InTenantAsync(parent, async services =>
        {
            var events = await services.GetRequiredService<ISession>()
                .Query<AuditTrailEvent, AuditTrailEventIndex>(
                    index => index.Category == HierarchyAuditEventNames.Category && index.CorrelationId == childEntryId,
                    collection: AuditTrailEvent.Collection)
                .OrderByDescending(index => index.CreatedUtc)
                .ListAsync();

            return events
                .Select(auditTrailEvent => (auditTrailEvent, auditTrailEvent.TryGet<HierarchyAuditEvent>(out var data) ? data : null))
                .ToList();
        });
    }
}
