using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Records and reads the hierarchy activity log of the current parent tenant.
/// </summary>
public sealed class HierarchyAuditLog
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchyAuditLog"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    /// <param name="clock">The clock.</param>
    public HierarchyAuditLog(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Records an event. The time is set by the log.
    /// </summary>
    /// <param name="auditEvent">The event.</param>
    public Task RecordAsync(HierarchyAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        auditEvent.CreatedUtc = _clock.UtcNow;

        return _session.SaveAsync(auditEvent, checkConcurrency: false, Collection);
    }

    /// <summary>
    /// Counts the events, optionally for one child tenant.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant, or <see langword="null"/> for every event.</param>
    public Task<int> CountAsync(string childEntryId)
    {
        return Query(childEntryId).CountAsync();
    }

    /// <summary>
    /// Returns a page of events, most recent first, optionally for one child tenant.
    /// </summary>
    /// <param name="childEntryId">The registry entry of the child tenant, or <see langword="null"/> for every event.</param>
    /// <param name="skip">The number of events to skip.</param>
    /// <param name="take">The number of events to return.</param>
    public Task<IReadOnlyList<HierarchyAuditEvent>> PageAsync(string childEntryId, int skip, int take)
    {
        return Query(childEntryId)
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Skip(skip)
            .Take(take)
            .ListAsync();
    }

    private IQuery<HierarchyAuditEvent, HierarchyAuditEventIndex> Query(string childEntryId)
    {
        return string.IsNullOrEmpty(childEntryId)
            ? _session.Query<HierarchyAuditEvent, HierarchyAuditEventIndex>(Collection)
            : _session.Query<HierarchyAuditEvent, HierarchyAuditEventIndex>(index => index.ChildEntryId == childEntryId, Collection);
    }
}
