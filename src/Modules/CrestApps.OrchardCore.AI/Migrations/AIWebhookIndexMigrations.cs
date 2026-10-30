using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the index tables of the AI Webhooks feature.
/// </summary>
internal sealed class AIWebhookIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIWebhookIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the subscription and delivery tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await AIWebhookIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
