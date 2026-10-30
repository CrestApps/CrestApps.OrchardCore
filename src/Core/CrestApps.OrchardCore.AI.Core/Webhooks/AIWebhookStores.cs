using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// Stores webhook subscriptions.
/// </summary>
public interface IAIWebhookSubscriptionStore : ICatalog<AIWebhookSubscription>
{
    /// <summary>
    /// Lists the subscriptions that send events.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The subscriptions.</returns>
    Task<IReadOnlyList<AIWebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores webhook deliveries.
/// </summary>
public interface IAIWebhookDeliveryStore : ICatalog<AIWebhookDelivery>
{
    /// <summary>
    /// Lists the deliveries whose next attempt is due, oldest first.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="take">How many to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deliveries.</returns>
    Task<IReadOnlyList<AIWebhookDelivery>> ListDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of a subscription's deliveries, newest first.
    /// </summary>
    /// <param name="subscriptionId">The subscription.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deliveries and how many there are.</returns>
    Task<(IReadOnlyList<AIWebhookDelivery> Deliveries, int Count)> PageAsync(string subscriptionId, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds webhook subscriptions by name and status.
/// </summary>
public sealed class AIWebhookSubscriptionIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether events are sent.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Finds webhook deliveries by subscription, status and time.
/// </summary>
public sealed class AIWebhookDeliveryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the subscription.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets where the delivery stands.
    /// </summary>
    public AIWebhookDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the event happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the next attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}

/// <summary>
/// Maps webhook subscriptions to their index.
/// </summary>
public sealed class AIWebhookSubscriptionIndexProvider : IndexProvider<AIWebhookSubscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookSubscriptionIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIWebhookSubscriptionIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIWebhookSubscription> context)
    {
        context.For<AIWebhookSubscriptionIndex>()
            .Map(subscription => new AIWebhookSubscriptionIndex
            {
                ItemId = subscription.ItemId,
                Name = subscription.Name,
                Enabled = subscription.Enabled,
            });
    }
}

/// <summary>
/// Maps webhook deliveries to their index.
/// </summary>
public sealed class AIWebhookDeliveryIndexProvider : IndexProvider<AIWebhookDelivery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookDeliveryIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIWebhookDeliveryIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIWebhookDelivery> context)
    {
        context.For<AIWebhookDeliveryIndex>()
            .Map(delivery => new AIWebhookDeliveryIndex
            {
                ItemId = delivery.ItemId,
                SubscriptionId = delivery.SubscriptionId,
                Status = delivery.Status,
                CreatedUtc = delivery.CreatedUtc,
                NextAttemptUtc = delivery.NextAttemptUtc,
            });
    }
}

/// <summary>
/// Creates the webhook index tables.
/// </summary>
public static class AIWebhookIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIWebhookSubscriptionIndex>(table => table
            .Column<string>(nameof(AIWebhookSubscriptionIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIWebhookSubscriptionIndex.Name), column => column.WithLength(255))
            .Column<bool>(nameof(AIWebhookSubscriptionIndex.Enabled)),
            collection: options.AICollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AIWebhookDeliveryIndex>(table => table
            .Column<string>(nameof(AIWebhookDeliveryIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIWebhookDeliveryIndex.SubscriptionId), column => column.WithLength(26))
            .Column<int>(nameof(AIWebhookDeliveryIndex.Status))
            .Column<DateTime>(nameof(AIWebhookDeliveryIndex.CreatedUtc))
            .Column<DateTime>(nameof(AIWebhookDeliveryIndex.NextAttemptUtc)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIWebhookDeliveryIndex>(
            table => table.CreateIndex("IDX_AIWebhookDelivery_Due", nameof(AIWebhookDeliveryIndex.Status), nameof(AIWebhookDeliveryIndex.NextAttemptUtc), "DocumentId"),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIWebhookDeliveryIndex>(
            table => table.CreateIndex("IDX_AIWebhookDelivery_Subscription", nameof(AIWebhookDeliveryIndex.SubscriptionId), nameof(AIWebhookDeliveryIndex.CreatedUtc), "DocumentId"),
            collection: options.AICollectionName);
    }
}

/// <summary>
/// Stores webhook subscriptions in the AI collection.
/// </summary>
public sealed class AIWebhookSubscriptionStore : DocumentCatalog<AIWebhookSubscription, AIWebhookSubscriptionIndex>, IAIWebhookSubscriptionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookSubscriptionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIWebhookSubscriptionStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIWebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = await Session.Query<AIWebhookSubscription, AIWebhookSubscriptionIndex>(index => index.Enabled, collection: CollectionName)
            .ListAsync(cancellationToken);

        return subscriptions.ToList();
    }
}

/// <summary>
/// Stores webhook deliveries in the AI collection.
/// </summary>
public sealed class AIWebhookDeliveryStore : DocumentCatalog<AIWebhookDelivery, AIWebhookDeliveryIndex>, IAIWebhookDeliveryStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookDeliveryStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIWebhookDeliveryStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIWebhookDelivery>> ListDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var deliveries = await Session.Query<AIWebhookDelivery, AIWebhookDeliveryIndex>(index => index.Status == AIWebhookDeliveryStatus.Pending && index.NextAttemptUtc <= nowUtc, collection: CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return deliveries.ToList();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AIWebhookDelivery> Deliveries, int Count)> PageAsync(string subscriptionId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<AIWebhookDelivery, AIWebhookDeliveryIndex>(index => index.SubscriptionId == subscriptionId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var deliveries = await query.OrderByDescending(index => index.CreatedUtc).Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken);

        return (deliveries.ToList(), count);
    }
}
