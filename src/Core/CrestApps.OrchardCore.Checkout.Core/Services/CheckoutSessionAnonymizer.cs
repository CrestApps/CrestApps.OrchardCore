using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutSessionAnonymizer"/>. It finds the sessions through the
/// <see cref="ICheckoutSessionStore"/> and anonymizes each with <see cref="CheckoutSession.Anonymize"/>.
/// </summary>
public sealed class CheckoutSessionAnonymizer : ICheckoutSessionAnonymizer
{
    internal const int PageSize = 50;

    /// <summary>
    /// The most sessions one reference is anonymized for. A reference has a handful of attempts; the cap only keeps a
    /// runaway reference from holding the request.
    /// </summary>
    internal const int MaxSessions = 1_000;

    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutSessionAnonymizer"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="clock">The clock the anonymization is stamped with.</param>
    public CheckoutSessionAnonymizer(ICheckoutSessionStore sessionStore, IClock clock)
    {
        _sessionStore = sessionStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> AnonymizeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        var sessions = await _sessionStore.GetManyAsync([sessionId], cancellationToken);

        return sessions.Count > 0 && await AnonymizeAsync(sessions[0], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> AnonymizeByReferenceAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceType);
        ArgumentException.ThrowIfNullOrEmpty(referenceId);

        var count = 0;

        // Anonymizing leaves the reference alone, so the pages do not shift while they are worked through.
        for (var skip = 0; skip < MaxSessions; skip += PageSize)
        {
            var sessions = await _sessionStore.ListByReferenceAsync(referenceType, referenceId, skip, PageSize, cancellationToken);

            foreach (var session in sessions)
            {
                if (await AnonymizeAsync(session, cancellationToken))
                {
                    count++;
                }
            }

            if (sessions.Count < PageSize)
            {
                break;
            }
        }

        return count;
    }

    private async Task<bool> AnonymizeAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        if (session.AnonymizedUtc is not null)
        {
            return false;
        }

        session.Anonymize(_clock.UtcNow);

        await _sessionStore.SaveAsync(session, cancellationToken);

        return true;
    }
}
