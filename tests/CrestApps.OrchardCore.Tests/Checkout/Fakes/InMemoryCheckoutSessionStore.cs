using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Checkout.Fakes;

/// <summary>
/// An in-memory <see cref="ICheckoutSessionStore"/> that runs the registered handlers on activation, mirroring
/// the real store, so engine tests exercise the same step and invoice construction the runtime does.
/// </summary>
internal sealed class InMemoryCheckoutSessionStore : ICheckoutSessionStore
{
    private readonly Dictionary<string, CheckoutSession> _sessions = new(StringComparer.Ordinal);
    private readonly IEnumerable<ICheckoutHandler> _handlers;

    public InMemoryCheckoutSessionStore(IEnumerable<ICheckoutHandler> handlers = null)
    {
        _handlers = handlers ?? [];
    }

    /// <summary>
    /// Gets the number of times a session was saved, so a test can assert that a transition was persisted.
    /// </summary>
    public int SaveCount { get; private set; }

    public void Seed(CheckoutSession session)
        => _sessions[session.SessionId] = session;

    public Task<CheckoutSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_sessions.GetValueOrDefault(sessionId));

    public Task<CheckoutSession> GetAsync(string sessionId, CheckoutSessionStatus status, CancellationToken cancellationToken = default)
    {
        var session = _sessions.GetValueOrDefault(sessionId);

        return Task.FromResult(session?.Status == status ? session : null);
    }

    public Task<CheckoutSession> GetByReferenceAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_sessions.Values
            .Where(session => session.ReferenceType == referenceType && session.ReferenceId == referenceId)
            .OrderByDescending(session => session.CreatedUtc)
            .FirstOrDefault());

    public Task<IReadOnlyList<CheckoutSession>> ListByReferenceAsync(string referenceType, string referenceId, int skip, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CheckoutSession>>(_sessions.Values
            .Where(session => session.ReferenceType == referenceType && session.ReferenceId == referenceId)
            .OrderBy(session => session.CreatedUtc)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .Skip(skip)
            .Take(take)
            .ToArray());

    public Task<IReadOnlyList<CheckoutSession>> GetByCustomerAsync(string ownerId, string email, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ownerId) && string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<IReadOnlyList<CheckoutSession>>([]);
        }

        var matches = _sessions.Values
            .Where(session =>
                (!string.IsNullOrEmpty(ownerId) && string.Equals(session.OwnerId, ownerId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(email) &&
                    session.TryGet<CheckoutContactInfo>(out var contact) &&
                    string.Equals(contact.Email?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(session => session.CreatedUtc)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .Skip(skip)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<CheckoutSession>>(matches);
    }

    public Task<IReadOnlyList<CheckoutSession>> GetManyAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CheckoutSession>>(sessionIds
            .Where(id => id is not null && _sessions.ContainsKey(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => _sessions[id])
            .ToArray());

    public async Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, Action<CheckoutSession> configure = null, CancellationToken cancellationToken = default)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-" + (_sessions.Count + 1),
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceVersionId = referenceVersionId,
            Status = CheckoutSessionStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
        };

        // Mirrors the real store: whatever decides what is being bought is on the session before the
        // handlers build the flow from it.
        configure?.Invoke(session);

        var activating = new CheckoutFlowActivatingContext(session);

        foreach (var handler in _handlers)
        {
            await handler.ActivatingAsync(activating);
        }

        if (activating.Errors.Count > 0)
        {
            throw new CheckoutRejectedException(activating.Errors);
        }

        var flow = new CheckoutFlow(session);

        session.CurrentStep = flow.GetFirstStep()?.Key;

        foreach (var handler in _handlers)
        {
            await handler.ActivatedAsync(new CheckoutFlowActivatedContext(flow));
        }

        return session;
    }

    public Task<IReadOnlyList<CheckoutSession>> GetStaleAsync(CheckoutSessionStatus status, DateTime modifiedBeforeUtc, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CheckoutSession>>(_sessions.Values
            .Where(session => session.Status == status && session.ModifiedUtc < modifiedBeforeUtc)
            .OrderBy(session => session.ModifiedUtc)
            .Take(take)
            .ToArray());

    public Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        _sessions[session.SessionId] = session;

        return Task.CompletedTask;
    }
}
