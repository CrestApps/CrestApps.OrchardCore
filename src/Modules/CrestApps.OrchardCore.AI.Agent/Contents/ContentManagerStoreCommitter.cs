using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Commits the YesSql session and resets the content manager's cache with it. A tool that generates a file commits partway through
/// an AI turn, which ends the session's tracking of every item but leaves them in the cache. A later tool that saved a cached item
/// would throw "An object with the same identity is already part of this transaction".
/// </summary>
public sealed class ContentManagerStoreCommitter : IStoreCommitter
{
    private readonly ISession _session;
    private readonly IContentManagerSession _contentManagerSession;
    private readonly ILogger<ContentManagerStoreCommitter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentManagerStoreCommitter"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManagerSession">The content manager's cache.</param>
    /// <param name="logger">The logger.</param>
    public ContentManagerStoreCommitter(ISession session, IContentManagerSession contentManagerSession, ILogger<ContentManagerStoreCommitter> logger)
    {
        _session = session;
        _contentManagerSession = contentManagerSession;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Committed the YesSql session.");
        }
        finally
        {
            // The session stops tracking its items whether or not the commit succeeds.
            _contentManagerSession.Clear();

            _logger.LogDebug("Cleared the content manager's cache after the commit.");
        }
    }
}
