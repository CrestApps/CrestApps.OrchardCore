using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AICompletionUsageIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionUsageIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AICompletionUsageIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAICompletionUsageIndexSchemaAsync(_option);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read the usage records through, newest first by <c>CreatedUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<AICompletionUsageIndex>(
            SchemaBuilder,
            "IDX_AICompletionUsageIndex_Report",
            _option.AICollectionName,
            "CreatedUtc", "Id");
    }
}
