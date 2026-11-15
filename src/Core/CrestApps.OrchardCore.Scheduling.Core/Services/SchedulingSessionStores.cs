using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Stores dated sessions.
/// </summary>
public sealed class SchedulingSessionStore : DocumentCatalog<SchedulingSession, SchedulingSessionIndex>
{
    public SchedulingSessionStore(ISession session)
        : base(session)
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    public async Task<IReadOnlyList<SchedulingSession>> ListBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
        => (await Session.Query<SchedulingSession, SchedulingSessionIndex>(index => index.Source == source && index.SourceId == sourceId, collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .ListAsync(cancellationToken)).ToArray();

    public async Task<IReadOnlyList<SchedulingSession>> ListStartingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => (await Session.Query<SchedulingSession, SchedulingSessionIndex>(index => index.StartUtc >= fromUtc && index.StartUtc < toUtc, collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .ListAsync(cancellationToken)).ToArray();

    public async Task<SchedulingSession> FindFreshByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var found = await FindByIdAsync(id, cancellationToken);

        if (found is null)
        {
            return null;
        }

        Session.Detach(found, CollectionName);

        return await FindByIdAsync(id, cancellationToken);
    }
}

/// <summary>
/// Reads the registrations and the waitlist of sessions.
/// </summary>
public sealed class SessionRegistrationStore
{
    private readonly ISession _session;

    public SessionRegistrationStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<Appointment>> ListAsync(string sessionId, CancellationToken cancellationToken = default)
        => (await _session.Query<Appointment, SessionRegistrationIndex>(index => index.SessionId == sessionId, collection: SchedulingConstants.CollectionName)
            .ListAsync(cancellationToken)).OrderBy(appointment => appointment.CreatedUtc).ToArray();

    public Task<int> CountTakenAsync(string sessionId, CancellationToken cancellationToken = default)
        => _session.QueryIndex<SessionRegistrationIndex>(
            index => index.SessionId == sessionId && index.Status.IsIn(new[] { AppointmentStatus.Held, AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow }),
            collection: SchedulingConstants.CollectionName).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListExpiredOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        => (await _session.Query<Appointment, SessionRegistrationIndex>(
            index => index.Status == AppointmentStatus.Held && index.HoldExpiresUtc != null && index.HoldExpiresUtc <= nowUtc,
            collection: SchedulingConstants.CollectionName).ListAsync(cancellationToken)).ToArray();

    public async Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string sessionId, CancellationToken cancellationToken = default)
        => (await _session.Query<WaitlistEntry, SessionWaitlistIndex>(index => index.SessionId == sessionId, collection: SchedulingConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken)).ToArray();

    public async Task<WaitlistEntry> FindFirstWaitingAsync(string sessionId, CancellationToken cancellationToken = default)
        => await _session.Query<WaitlistEntry, SessionWaitlistIndex>(index => index.SessionId == sessionId && index.Status == WaitlistStatus.Waiting, collection: SchedulingConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
