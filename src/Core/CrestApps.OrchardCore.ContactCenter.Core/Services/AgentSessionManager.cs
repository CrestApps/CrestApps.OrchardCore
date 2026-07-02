using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentSessionManager"/> that delegates storage to
/// <see cref="IAgentSessionStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class AgentSessionManager : CatalogManager<AgentSession>, IAgentSessionManager
{
    private readonly IAgentSessionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying agent session store.</param>
    /// <param name="handlers">The catalog entry handlers for agent sessions.</param>
    /// <param name="logger">The logger instance.</param>
    public AgentSessionManager(
        IAgentSessionStore store,
        IEnumerable<ICatalogEntryHandler<AgentSession>> handlers,
        ILogger<CatalogManager<AgentSession>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var session = await _store.FindByUserIdAsync(userId, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<AgentSession>> ListStaleAsync(DateTime heartbeatCutoffUtc, CancellationToken cancellationToken = default)
    {
        var sessions = await _store.ListStaleAsync(heartbeatCutoffUtc, cancellationToken);

        foreach (var session in sessions)
        {
            await LoadAsync(session, cancellationToken);
        }

        return sessions;
    }
}
