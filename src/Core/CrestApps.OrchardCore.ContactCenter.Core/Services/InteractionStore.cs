using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionStore"/>.
/// </summary>
public sealed class InteractionStore : DocumentCatalog<Interaction, InteractionIndex>, IInteractionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public InteractionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.ActivityItemId == activityItemId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.CorrelationId == correlationId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByProviderInteractionIdAsync(string providerInteractionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerInteractionId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.ProviderInteractionId == providerInteractionId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Interaction, InteractionIndex>(
            index => index.Status == status,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<Interaction>
        {
            Count = await query.CountAsync(cancellationToken),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }
}
