using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IActivityReservationStore"/>.
/// </summary>
public sealed class ActivityReservationStore : DocumentCatalog<ActivityReservation, ActivityReservationIndex>, IActivityReservationStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ActivityReservationStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ExpiredReservationPage> ListExpiredAsync(
        DateTime utcNow,
        DateTime? afterExpiresUtc,
        long afterDocumentId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // Keyset (seek) paging over the stable (ExpiresUtc, DocumentId) order. Unlike offset paging, the
        // cursor is an absolute position in the key space, so concurrent expirations or insertions elsewhere
        // in the backlog never shift the window and cause a live reservation to be skipped. The index is
        // queried (not the documents) because the keyset tiebreaker is the YesSql DocumentId, which the
        // reservation document itself does not carry.
        IQueryIndex<ActivityReservationIndex> indexQuery;

        if (afterExpiresUtc.HasValue)
        {
            var cursorExpiresUtc = afterExpiresUtc.Value;

            indexQuery = Session.QueryIndex<ActivityReservationIndex>(
                index => index.Status == ReservationStatus.Pending
                    && index.ExpiresUtc <= utcNow
                    && (index.ExpiresUtc > cursorExpiresUtc
                        || (index.ExpiresUtc == cursorExpiresUtc && index.DocumentId > afterDocumentId)),
                collection: ContactCenterStorage.CollectionName);
        }
        else
        {
            indexQuery = Session.QueryIndex<ActivityReservationIndex>(
                index => index.Status == ReservationStatus.Pending && index.ExpiresUtc <= utcNow,
                collection: ContactCenterStorage.CollectionName);
        }

        var indexRows = (await indexQuery
            .OrderBy(index => index.ExpiresUtc)
            .ThenBy(index => index.DocumentId)
            .Take(maxResults)
            .ListAsync(cancellationToken)).ToArray();

        if (indexRows.Length == 0)
        {
            return new ExpiredReservationPage([], null, 0);
        }

        var documentIds = indexRows.Select(index => index.DocumentId).ToArray();

        var documents = await Session.GetAsync<ActivityReservation>(
            documentIds,
            collection: ContactCenterStorage.CollectionName,
            cancellationToken);

        // A full page means there may be more work behind it, so surface the cursor. A short page means the
        // backlog is exhausted for this run; leaving the cursor null tells the caller to stop.
        var hasMore = indexRows.Length == maxResults;
        var lastRow = indexRows[indexRows.Length - 1];

        return new ExpiredReservationPage(
            documents.ToArray(),
            hasMore ? lastRow.ExpiresUtc : null,
            hasMore ? lastRow.DocumentId : 0);
    }


    /// <inheritdoc/>
    public async Task<ActivityReservation> FindPendingByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.AgentId == agentId && index.Status == ReservationStatus.Pending,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityReservation>> ListActiveByAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var reservations = await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.AgentId == agentId &&
                (index.Status == ReservationStatus.Pending || index.Status == ReservationStatus.Accepted),
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return reservations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityReservation>> ListActiveByActivityAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        var reservations = await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.ActivityItemId == activityItemId &&
                (index.Status == ReservationStatus.Pending || index.Status == ReservationStatus.Accepted),
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return reservations.ToArray();
    }
}
