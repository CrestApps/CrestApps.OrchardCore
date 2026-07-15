using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ICallSessionStore"/>.
/// </summary>
public sealed class CallSessionStore : DocumentCatalog<CallSession, CallSessionIndex>, ICallSessionStore
{
    /// <summary>
    /// Gets a value indicating that call session updates use YesSql document-version concurrency checks so
    /// concurrent provider-event ingestion cannot lose or reverse a high-water/state update. A losing writer
    /// observes a <see cref="ConcurrencyException"/> instead of silently overwriting a newer commit.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CallSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.ProviderCallId == providerCallId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.ProviderName == providerName &&
                index.ProviderCallId == providerCallId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
