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
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityReservation>> ListExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var reservations = await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.Status == ReservationStatus.Pending && index.ExpiresUtc <= utcNow,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return reservations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> FindPendingByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.AgentId == agentId && index.Status == ReservationStatus.Pending,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityReservation>> ListActiveByActivityAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        var reservations = await Session.Query<ActivityReservation, ActivityReservationIndex>(
            index => index.ActivityItemId == activityItemId &&
                (index.Status == ReservationStatus.Pending || index.Status == ReservationStatus.Accepted),
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return reservations.ToArray();
    }
}
