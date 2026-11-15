using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Stores certification paths.
/// </summary>
public sealed class CertificationPathStore : DocumentCatalog<CertificationPath, CertificationPathIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationPathStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public CertificationPathStore(ISession session)
        : base(session)
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <summary>
    /// Finds the path of a certification.
    /// </summary>
    public Task<CertificationPath> FindByCertificationAsync(string certificationId, CancellationToken cancellationToken = default)
    {
        return Session.Query<CertificationPath, CertificationPathIndex>(index => index.CertificationId == certificationId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the enabled paths about a course or quiz.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPath>> ListByTargetAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<CertificationPathTargetIndex>(index => index.TargetId == targetId && index.Enabled, collection: CollectionName)
            .ListAsync(cancellationToken);
        var ids = rows.Select(row => row.PathId).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return (await Session.Query<CertificationPath, CertificationPathIndex>(index => index.ItemId.IsIn(ids), collection: CollectionName).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists every path.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPath>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return (await Session.Query<CertificationPath, CertificationPathIndex>(collection: CollectionName).ListAsync(cancellationToken)).ToArray();
    }
}

/// <summary>
/// Stores the progress of people on certification paths.
/// </summary>
public sealed class CertificationPathProgressStore : DocumentCatalog<CertificationPathProgress, CertificationPathProgressIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationPathProgressStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public CertificationPathProgressStore(ISession session)
        : base(session)
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <summary>
    /// Finds a person's progress on a path, reading it again from the store.
    /// </summary>
    public async Task<CertificationPathProgress> FindFreshAsync(string pathId, string userId, CancellationToken cancellationToken = default)
    {
        var progress = await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.PathId == pathId && index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (progress is null)
        {
            return null;
        }

        // Another scope may have changed it since this session read it: read what was committed.
        Session.Detach(progress, CollectionName);

        return await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.PathId == pathId && index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds progress by id, reading it again from the store.
    /// </summary>
    public async Task<CertificationPathProgress> FindFreshByIdAsync(string progressId, CancellationToken cancellationToken = default)
    {
        var progress = await FindByIdAsync(progressId, cancellationToken);

        if (progress is null)
        {
            return null;
        }

        Session.Detach(progress, CollectionName);

        return await FindByIdAsync(progressId, cancellationToken);
    }

    /// <summary>
    /// Lists a person's progress.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPathProgress>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.UserId == userId, collection: CollectionName).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists the progress whose current certificate is a record.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPathProgress>> ListByRecordAsync(string recordId, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.CurrentRecordId == recordId, collection: CollectionName).ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Lists the progress of a path in some states.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPathProgress>> ListByStatesAsync(string pathId, IReadOnlyCollection<CertificationPathState> states, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.PathId == pathId && index.State.IsIn(states), collection: CollectionName)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Pages the people working on a path in some states, in the order the rows were written, after a row.
    /// </summary>
    /// <param name="pathId">The path.</param>
    /// <param name="states">The states.</param>
    /// <param name="afterId">The index row to start after; 0 for the first page.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each person with the index row it came from.</returns>
    public async Task<IReadOnlyList<(string UserId, long RowId)>> PageUserIdsByStatesAsync(string pathId, IReadOnlyCollection<CertificationPathState> states, long afterId, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<CertificationPathProgressIndex>(index => index.PathId == pathId && index.State.IsIn(states) && index.Id > afterId, collection: CollectionName)
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);

        return rows.Select(row => (row.UserId, row.Id)).ToArray();
    }

    /// <summary>
    /// Pages the certified people of some paths whose renewal should be open by a day, after an index row.
    /// </summary>
    /// <param name="pathIds">The paths to look at: the enabled ones.</param>
    /// <param name="onOrBeforeKey">The day, as yyyyMMdd.</param>
    /// <param name="afterId">The index row to start after; 0 for the first page.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Each progress id with the index row it came from.</returns>
    public async Task<IReadOnlyList<(string ProgressId, long RowId)>> PageRenewalDueAsync(IReadOnlyCollection<string> pathIds, int onOrBeforeKey, long afterId, int take, CancellationToken cancellationToken = default)
    {
        if (pathIds.Count == 0)
        {
            return [];
        }

        var rows = await Session.QueryIndex<CertificationPathProgressIndex>(
            index => index.PathId.IsIn(pathIds) && index.State == CertificationPathState.Certified && index.RenewalOpensOn != null && index.RenewalOpensOn <= onOrBeforeKey && index.Id > afterId,
            collection: CollectionName)
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);

        return rows.Select(row => (row.ItemId, row.Id)).ToArray();
    }

    /// <summary>
    /// Lists certified people whose renewal should be open by a day.
    /// </summary>
    public async Task<IReadOnlyList<CertificationPathProgress>> ListRenewalDueAsync(int onOrBeforeKey, int take, CancellationToken cancellationToken = default)
    {
        return (await Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(
            index => index.State == CertificationPathState.Certified && index.RenewalOpensOn != null && index.RenewalOpensOn <= onOrBeforeKey,
            collection: CollectionName)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <summary>
    /// Pages the progress of a path, by state, soonest expiry first.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<CertificationPathProgress> Items)> PageAsync(string pathId, CertificationPathState? state, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<CertificationPathProgress, CertificationPathProgressIndex>(index => index.PathId == pathId, collection: CollectionName);

        if (state is { } value)
        {
            query = query.Where(index => index.State == value);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.ExpiresOn).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }
}
