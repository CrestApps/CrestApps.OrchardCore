using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ICallSessionManager"/> that delegates storage to
/// <see cref="ICallSessionStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class CallSessionManager : CatalogManager<CallSession>, ICallSessionManager
{
    private readonly ICallSessionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying call session store.</param>
    /// <param name="handlers">The catalog entry handlers for call sessions.</param>
    /// <param name="logger">The logger instance.</param>
    public CallSessionManager(
        ICallSessionStore store,
        IEnumerable<ICatalogEntryHandler<CallSession>> handlers,
        ILogger<CatalogManager<CallSession>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        var session = await _store.FindByProviderCallIdAsync(providerCallId, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        var session = await _store.FindByProviderCallIdAsync(providerName, providerCallId, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        var session = await _store.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }
}
