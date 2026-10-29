using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Finds AI test cases by name and by the profile they test.
/// </summary>
public sealed class AITestCaseIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the test's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the profile under test.
    /// </summary>
    public string ProfileId { get; set; }
}

/// <summary>
/// Finds AI test runs by test, batch and status, newest first.
/// </summary>
public sealed class AITestRunIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the test that ran.
    /// </summary>
    public string TestCaseId { get; set; }

    /// <summary>
    /// Gets or sets the batch the run belongs to.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets where the run stands.
    /// </summary>
    public AITestRunStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the run was queued.
    /// </summary>
    public DateTime QueuedUtc { get; set; }
}

/// <summary>
/// Maps AI test cases and runs to their indexes.
/// </summary>
public sealed class AITestIndexProvider : IndexProvider<AITestCase>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AITestIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AITestIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AITestCase> context)
    {
        context.For<AITestCaseIndex>()
            .Map(testCase => new AITestCaseIndex
            {
                ItemId = testCase.ItemId,
                Name = testCase.Name,
                ProfileId = testCase.ProfileId,
            });
    }
}

/// <summary>
/// Maps AI test runs to their index.
/// </summary>
public sealed class AITestRunIndexProvider : IndexProvider<AITestRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AITestRunIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AITestRunIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AITestRun> context)
    {
        context.For<AITestRunIndex>()
            .Map(run => new AITestRunIndex
            {
                ItemId = run.ItemId,
                TestCaseId = run.TestCaseId,
                BatchId = run.BatchId,
                Status = run.Status,
                QueuedUtc = run.QueuedUtc,
            });
    }
}

/// <summary>
/// Creates the AI test index tables.
/// </summary>
public static class AITestIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<AITestCaseIndex>(table => table
            .Column<string>(nameof(AITestCaseIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AITestCaseIndex.Name), column => column.WithLength(255))
            .Column<string>(nameof(AITestCaseIndex.ProfileId), column => column.WithLength(26)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AITestCaseIndex>(
            table => table.CreateIndex("IDX_AITestCase_ItemId", nameof(AITestCaseIndex.ItemId), "DocumentId"),
            collection: options.AICollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AITestRunIndex>(table => table
            .Column<string>(nameof(AITestRunIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AITestRunIndex.TestCaseId), column => column.WithLength(26))
            .Column<string>(nameof(AITestRunIndex.BatchId), column => column.WithLength(26))
            .Column<int>(nameof(AITestRunIndex.Status))
            .Column<DateTime>(nameof(AITestRunIndex.QueuedUtc)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AITestRunIndex>(
            table => table.CreateIndex("IDX_AITestRun_TestCase", nameof(AITestRunIndex.TestCaseId), nameof(AITestRunIndex.QueuedUtc), "DocumentId"),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AITestRunIndex>(
            table => table.CreateIndex("IDX_AITestRun_Status", nameof(AITestRunIndex.Status), nameof(AITestRunIndex.QueuedUtc), "DocumentId"),
            collection: options.AICollectionName);
    }
}
