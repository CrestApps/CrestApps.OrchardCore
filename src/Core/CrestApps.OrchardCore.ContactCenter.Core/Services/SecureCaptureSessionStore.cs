using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ISecureCaptureSessionStore"/>.
/// </summary>
public sealed class SecureCaptureSessionStore : DocumentCatalog<SecureCaptureSession, SecureCaptureSessionIndex>, ISecureCaptureSessionStore
{
    private const int DefaultBatchSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SecureCaptureSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public async Task<SecureCaptureSession> FindByAccessTokenHashAsync(string accessTokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accessTokenHash))
        {
            return null;
        }

        return await Session.Query<SecureCaptureSession, SecureCaptureSessionIndex>(
            index => index.AccessTokenHash == accessTokenHash,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SecureCaptureSession>> ListExpiredAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var sessions = await Session.Query<SecureCaptureSession, SecureCaptureSessionIndex>(
            index => index.State == SecureCaptureState.Collecting && index.ExpiresUtc <= utcNow,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return sessions.ToArray();
    }

    /// <inheritdoc/>
    public async Task<SecureCaptureSession> FindActiveByInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return null;
        }

        return await Session.Query<SecureCaptureSession, SecureCaptureSessionIndex>(
            index => index.InteractionId == interactionId && index.State == SecureCaptureState.Collecting,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SecureCaptureSession>> ListPendingRecordingResumeAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var sessions = await Session.Query<SecureCaptureSession, SecureCaptureSessionIndex>(
            index => index.State != SecureCaptureState.Collecting
                && index.EngagedRecordingPause
                && !index.RecordingResumed,
            collection: ContactCenterStorage.CollectionName)
            .Take(take)
            .ListAsync(cancellationToken);

        return sessions.ToArray();
    }
}
