using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the table AI conversation evaluations are reported and alerted on from.
/// </summary>
internal sealed class AIConversationEvaluationIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluationIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The store options that name the AI collection.</param>
    public AIConversationEvaluationIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await AIConversationEvaluationIndexSchema.CreateAsync(SchemaBuilder, _options);

        // Reports read the evaluations newest first a page at a time, so each page and date range is a seek.
        await IndexTableIndexCreation.CreateIfMissingAsync<AIConversationEvaluationIndex>(
            SchemaBuilder,
            "IDX_AIConversationEvaluationIndex_Report",
            _options.AICollectionName,
            "EvaluatedUtc", "Id");

        return 1;
    }
}
