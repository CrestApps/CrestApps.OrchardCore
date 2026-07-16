using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionStore"/>.
/// </summary>
public sealed class InteractionStore : DocumentCatalog<Interaction, InteractionIndex>, IInteractionStore
{
    private const int QueryBatchSize = 500;
    private const int DefaultReconciliationBatchSize = 200;

    /// <summary>
    /// Gets a value indicating that interaction updates use YesSql document-version concurrency checks so
    /// concurrent provider-event ingestion cannot lose or reverse a communication state update. A losing
    /// writer observes a <see cref="ConcurrencyException"/> instead of silently overwriting a newer commit.
    /// </summary>
    protected override bool CheckConcurrency => true;

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
    public async Task<Interaction> FindByProviderInteractionIdAsync(
        string providerName,
        string providerInteractionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(providerInteractionId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.ProviderName == providerName &&
                index.ProviderInteractionId == providerInteractionId,
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

    /// <inheritdoc/>
    public async Task<int> CountActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.AgentId == agentId &&
                index.Status != InteractionStatus.Created &&
                index.Status != InteractionStatus.Ended &&
                index.Status != InteractionStatus.Failed,
            collection: ContactCenterConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, int>> CountActiveByAgentIdsAsync(
        IReadOnlyCollection<string> agentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        if (agentIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var agentIdBatch in agentIds.Chunk(QueryBatchSize))
        {
            var indexes = await Session.QueryIndex<InteractionIndex>(
                index => index.AgentId.IsIn(agentIdBatch) &&
                    index.Status != InteractionStatus.Created &&
                    index.Status != InteractionStatus.Ended &&
                    index.Status != InteractionStatus.Failed,
                collection: ContactCenterConstants.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var group in indexes.GroupBy(index => index.AgentId, StringComparer.Ordinal))
            {
                counts[group.Key] = group.Count();
            }
        }

        return counts;
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return await Session.Query<Interaction, InteractionIndex>(
            index => index.AgentId == agentId &&
                index.Status != InteractionStatus.Created &&
                index.Status != InteractionStatus.Ended &&
                index.Status != InteractionStatus.Failed,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListPendingWrapUpsByAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return (await Session.Query<Interaction, InteractionIndex>(
            index => index.AgentId == agentId &&
                index.WrapUpStartedUtc != null &&
                index.WrapUpCompletedUtc == null,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.WrapUpStartedUtc)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListRecentByAgentAsync(string agentId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return (await Session.Query<Interaction, InteractionIndex>(
            index => index.AgentId == agentId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListActiveWithProviderCallIdAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultReconciliationBatchSize : maxCount;

        return (await Session.Query<Interaction, InteractionIndex>(
            index => index.Status != InteractionStatus.Ended &&
                index.Status != InteractionStatus.Failed,
            collection: ContactCenterConstants.CollectionName)
            .Where(index => index.ProviderInteractionId != null && index.ProviderInteractionId != string.Empty)
            .OrderBy(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Interaction>> ListActiveWithProviderCallIdAsync(
        string providerName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var take = maxCount <= 0 ? DefaultReconciliationBatchSize : maxCount;

        return (await Session.Query<Interaction, InteractionIndex>(
            index => index.ProviderName == providerName &&
                index.Status != InteractionStatus.Ended &&
                index.Status != InteractionStatus.Failed,
            collection: ContactCenterConstants.CollectionName)
            .Where(index => index.ProviderInteractionId != null && index.ProviderInteractionId != string.Empty)
            .OrderBy(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }
}
