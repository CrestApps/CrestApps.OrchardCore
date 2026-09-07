using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;

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

    public async Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default)
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

        foreach (var handler in _handlers)
        {
            await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));
        }

        var flow = new CheckoutFlow(session);

        session.CurrentStep = flow.GetFirstStep()?.Key;

        foreach (var handler in _handlers)
        {
            await handler.ActivatedAsync(new CheckoutFlowActivatedContext(flow));
        }

        return session;
    }

    public Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        _sessions[session.SessionId] = session;

        return Task.CompletedTask;
    }
}
