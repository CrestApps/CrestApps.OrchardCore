using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the index tables of the AI webhook records, and moves the subscriptions and pending deliveries those records
/// hold into the Webhooks building block, which sends AI events now.
/// </summary>
internal sealed class AIWebhookIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;
    private readonly AIWebhookImporter _importer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <param name="importer">Moves the AI webhook records into the Webhooks building block.</param>
    public AIWebhookIndexMigrations(IOptions<YesSqlStoreOptions> options, AIWebhookImporter importer)
    {
        _options = options.Value;
        _importer = importer;
    }

    /// <summary>
    /// Creates the subscription and delivery tables. A new tenant has no AI webhook records to move.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AIWebhookIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 2;
    }

    /// <summary>
    /// Moves the AI webhook subscriptions and pending deliveries into the Webhooks building block. It only adds records
    /// and settles the old ones, so the step is additive.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await _importer.ImportAsync();

        return 2;
    }
}
