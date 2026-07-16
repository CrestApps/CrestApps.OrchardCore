using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-backed provider webhook inbox store.
/// </summary>
public sealed class ProviderWebhookInboxStore : DocumentCatalog<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>, IProviderWebhookInboxStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    public ProviderWebhookInboxStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ProviderWebhookInboxMessage> FindByDeliveryAsync(
        string providerName,
        string deliveryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(deliveryId);

        return await Session.Query<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>(
            index => index.ProviderName == providerName && index.DeliveryId == deliveryId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderWebhookInboxMessage>> ListDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? ProviderWebhookInbox.MaxBatchSize : maxCount;
        var messages = await Session.Query<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>(
            index => (index.Status == ProviderWebhookInboxStatus.Pending || index.Status == ProviderWebhookInboxStatus.Claimed) &&
                index.NextAttemptUtc <= nowUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return messages.ToArray();
    }

    /// <inheritdoc/>
    public async Task<int> CountByStatusAsync(ProviderWebhookInboxStatus status, CancellationToken cancellationToken = default)
    {
        return await Session.Query<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>(
            index => index.Status == status,
            collection: ContactCenterConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await Session.Query<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>(
            index => (index.Status == ProviderWebhookInboxStatus.Pending || index.Status == ProviderWebhookInboxStatus.Claimed) &&
                index.NextAttemptUtc <= nowUtc,
            collection: ContactCenterConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderWebhookInboxMessage>> ListProcessedBeforeAsync(
        DateTime processedBeforeUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? ProviderWebhookInbox.MaxTombstoneCleanupBatchSize : maxCount;

        return (await Session.Query<ProviderWebhookInboxMessage, ProviderWebhookInboxMessageIndex>(
            index => index.Status == ProviderWebhookInboxStatus.Completed &&
                index.NextAttemptUtc < processedBeforeUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken)).ToArray();
    }
}
