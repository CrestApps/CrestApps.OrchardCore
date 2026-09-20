using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using CrestApps.Core.Telephony.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the provider webhook inbox index schema and enforces canonical provider-delivery uniqueness.
/// </summary>
internal sealed class ProviderWebhookInboxMessageIndexMigrations : DataMigration
{
    private readonly ProviderWebhookInboxMessageIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public ProviderWebhookInboxMessageIndexMigrations(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver,
        TimeProvider timeProvider)
    {
        _step = new ProviderWebhookInboxMessageIndexMigrationsSchemaMigration(store, providerIdentityResolver, timeProvider);
    }

    /// <summary>
    /// Creates the inbox index table and its lookup and due-message indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Canonicalizes legacy provider aliases and adds the canonical provider-delivery unique constraint to
    /// existing inbox indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the settlement time settled deliveries are purged by. Receipt time cannot serve, because settlement
    /// lags receipt by the whole retry envelope; the retry time cannot serve either, because a settled delivery
    /// keeps whatever retry time it last held.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
