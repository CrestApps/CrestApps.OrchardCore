using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionMetricsIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionMetricsIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public AIChatSessionMetricsIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        var options = new AIChatSessionMetricsIndexSchemaOptions
        {
            SessionIdLength = 26,
            ProfileIdLength = 26,
            VisitorIdLength = 64,
            UserIdLength = 26,
            CreateNamedIndexes = true,
        };

        await SchemaBuilder.CreateAIChatSessionMetricsSchemaAsync(_option, options);

        await CreateReportIndexAsync();

        return 5;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

    /// <summary>
    /// Updates the from2 async.
    /// </summary>
    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }

    /// <summary>
    /// Updates the from3 async.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AddAIChatSessionMetricsCompletionCountColumnAsync(_option);

        return 4;
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>SessionStartedUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await CreateReportIndexAsync();

        return 5;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<AIChatSessionMetricsIndex>(
            SchemaBuilder,
            "IDX_AIChatSessionMetricsIndex_Report",
            _option.AICollectionName,
            "SessionStartedUtc", "Id");
    }
}
