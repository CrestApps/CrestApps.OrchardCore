using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ISecureCaptureSessionManager"/>.
/// </summary>
public sealed class SecureCaptureSessionManager : CatalogManager<SecureCaptureSession>, ISecureCaptureSessionManager
{
    private readonly ISecureCaptureSessionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSessionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying secure capture session store.</param>
    /// <param name="handlers">The catalog entry handlers for secure capture sessions.</param>
    /// <param name="logger">The logger instance.</param>
    public SecureCaptureSessionManager(
        ISecureCaptureSessionStore store,
        IEnumerable<ICatalogEntryHandler<SecureCaptureSession>> handlers,
        ILogger<CatalogManager<SecureCaptureSession>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureSession> FindByAccessTokenHashAsync(string accessTokenHash, CancellationToken cancellationToken = default)
    {
        var session = await _store.FindByAccessTokenHashAsync(accessTokenHash, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SecureCaptureSession>> ListExpiredAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default)
    {
        var sessions = await _store.ListExpiredAsync(utcNow, maxCount, cancellationToken);

        foreach (var session in sessions)
        {
            await LoadAsync(session, cancellationToken);
        }

        return sessions;
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureSession> FindActiveByInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        var session = await _store.FindActiveByInteractionAsync(interactionId, cancellationToken);

        if (session is not null)
        {
            await LoadAsync(session, cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SecureCaptureSession>> ListPendingRecordingResumeAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var sessions = await _store.ListPendingRecordingResumeAsync(maxCount, cancellationToken);

        foreach (var session in sessions)
        {
            await LoadAsync(session, cancellationToken);
        }

        return sessions;
    }
}
