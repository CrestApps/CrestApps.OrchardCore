using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the table the per-call AI voice session summaries are reported from.
/// </summary>
internal sealed class AIVoiceSessionSummaryIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionSummaryIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The store options that name the AI collection.</param>
    public AIVoiceSessionSummaryIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await AIVoiceSessionSummaryIndexSchema.CreateAsync(SchemaBuilder, _options);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read the summaries through, newest first by <c>CreatedUtc</c> a page at a time, so each
    /// page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<AIVoiceSessionSummaryIndex>(
            SchemaBuilder,
            "IDX_AIVoiceSessionSummaryIndex_Report",
            _options.AICollectionName,
            "CreatedUtc", "Id");
    }
}
