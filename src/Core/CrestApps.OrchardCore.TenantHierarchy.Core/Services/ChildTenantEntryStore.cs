using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Stores the registry of child tenants in the database of the current parent tenant.
/// </summary>
public sealed class ChildTenantEntryStore
{
    private const string Collection = TenantHierarchyConstants.CollectionName;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantEntryStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the parent tenant.</param>
    public ChildTenantEntryStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds an entry by its identifier.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    public Task<ChildTenantEntry> FindByEntryIdAsync(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return Task.FromResult<ChildTenantEntry>(null);
        }

        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(index => index.EntryId == entryId, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds an entry by the tenant identifier of its child tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    public Task<ChildTenantEntry> FindByTenantIdAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return Task.FromResult<ChildTenantEntry>(null);
        }

        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(index => index.TenantId == tenantId, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds an entry by its slug.
    /// </summary>
    /// <param name="slug">The slug.</param>
    public Task<ChildTenantEntry> FindBySlugAsync(string slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return Task.FromResult<ChildTenantEntry>(null);
        }

        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(index => index.Slug == slug, Collection)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds the entries with the given identifiers.
    /// </summary>
    /// <param name="entryIds">The entry identifiers.</param>
    public async Task<IReadOnlyList<ChildTenantEntry>> GetAsync(IEnumerable<string> entryIds)
    {
        var ids = entryIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray() ?? [];

        if (ids.Length == 0)
        {
            return [];
        }

        return await _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(index => index.EntryId.IsIn(ids), Collection)
            .ListAsync();
    }

    /// <summary>
    /// Lists every entry, ordered by display name. A parent owns at most the number of child tenants its policy allows.
    /// </summary>
    public Task<IReadOnlyList<ChildTenantEntry>> ListAsync()
    {
        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(Collection)
            .OrderBy(index => index.DisplayName)
            .ListAsync();
    }

    /// <summary>
    /// Counts the entries.
    /// </summary>
    public Task<int> CountAsync()
    {
        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(Collection).CountAsync();
    }

    /// <summary>
    /// Lists the entries pending removal whose retention ended.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    public Task<IReadOnlyList<ChildTenantEntry>> ListDueForRemovalAsync(DateTime utcNow)
    {
        var status = nameof(ChildTenantStatus.PendingRemoval);

        return _session.Query<ChildTenantEntry, ChildTenantEntryIndex>(
            index => index.Status == status && index.RetainUntilUtc <= utcNow,
            Collection)
            .Take(50)
            .ListAsync();
    }

    /// <summary>
    /// Saves an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public Task SaveAsync(ChildTenantEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return _session.SaveAsync(entry, checkConcurrency: false, Collection);
    }

    /// <summary>
    /// Deletes an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public void Delete(ChildTenantEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _session.Delete(entry, Collection);
    }

    /// <summary>
    /// Commits the pending changes of the session.
    /// </summary>
    public Task SaveChangesAsync()
        => _session.SaveChangesAsync();
}
