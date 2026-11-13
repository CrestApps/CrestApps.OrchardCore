using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Stores service level timers.
/// </summary>
public sealed class ServiceLevelTimerStore : DocumentCatalog<ServiceLevelTimer, ServiceLevelTimerIndex>, IServiceLevelTimerStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelTimerStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ServiceLevelTimerStore(ISession session)
        : base(session)
    {
        CollectionName = ServiceLevelsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> ListBySubjectAsync(RecordRef subject, bool activeOnly, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return [];
        }

        var kind = subject.Kind;
        var id = subject.Id;

        var query = activeOnly
            ? Session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(index => index.SubjectKind == kind && index.SubjectId == id && index.IsActive, collection: CollectionName)
            : Session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(index => index.SubjectKind == kind && index.SubjectId == id, collection: CollectionName);

        return (await query.OrderByDescending(index => index.StartedUtc).ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyBreachedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var timers = await Session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(
                index => index.State == ServiceLevelTimerState.Running && !index.Breached && index.DueUtc <= nowUtc,
                collection: CollectionName)
            .OrderBy(index => index.DueUtc)
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken);

        return timers.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyWarnedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var timers = await Session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(
                index => index.State == ServiceLevelTimerState.Running && !index.Warned && !index.Breached && index.WarnAtUtc <= nowUtc && index.DueUtc > nowUtc,
                collection: CollectionName)
            .OrderBy(index => index.WarnAtUtc)
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken);

        return timers.ToArray();
    }

    /// <inheritdoc/>
    public Task UpdateIfUnchangedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timer);

        return Session.SaveAsync(timer, checkConcurrency: true, collection: CollectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);
}
