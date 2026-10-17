using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the YesSql implementation of <see cref="IDialerPacingCycleStore"/>.
/// </summary>
public sealed class DialerPacingCycleStore : DocumentCatalog<DialerPacingCycle, DialerPacingCycleIndex>, IDialerPacingCycleStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public DialerPacingCycleStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<DialerPacingCycle> FindLatestByQueueAsync(
        string queueId,
        IEnumerable<DialerPacingCycleOutcome> outcomes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var wanted = outcomes?.Distinct().ToArray();

        var query = wanted is null
            ? Session.Query<DialerPacingCycle, DialerPacingCycleIndex>(
                index => index.QueueId == queueId,
                collection: ContactCenterStorage.CollectionName)
            : Session.Query<DialerPacingCycle, DialerPacingCycleIndex>(
                index => index.QueueId == queueId && index.Outcome.IsIn(wanted),
                collection: ContactCenterStorage.CollectionName);

        return await query
            .OrderByDescending(index => index.CompletedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DialerPacingCycle> FindLatestByProfileAsync(
        string dialerProfileId,
        IEnumerable<DialerPacingCycleOutcome> outcomes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dialerProfileId);

        var wanted = outcomes?.Distinct().ToArray();

        var query = wanted is null
            ? Session.Query<DialerPacingCycle, DialerPacingCycleIndex>(
                index => index.DialerProfileId == dialerProfileId,
                collection: ContactCenterStorage.CollectionName)
            : Session.Query<DialerPacingCycle, DialerPacingCycleIndex>(
                index => index.DialerProfileId == dialerProfileId && index.Outcome.IsIn(wanted),
                collection: ContactCenterStorage.CollectionName);

        return await query
            .OrderByDescending(index => index.CompletedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<DialerPacingCycle>> GetCompletedBetweenAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<DialerPacingCycle, DialerPacingCycleIndex>(
            index => index.CompletedUtc >= fromUtc && index.CompletedUtc < toUtc,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.CompletedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }
}
