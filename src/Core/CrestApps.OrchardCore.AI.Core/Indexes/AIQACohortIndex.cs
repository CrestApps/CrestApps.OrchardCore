using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Finds QA cohorts by name and by whether they score conversations.
/// </summary>
public sealed class AIQACohortIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the cohort's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cohort scores conversations.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Maps QA cohorts to their index.
/// </summary>
public sealed class AIQACohortIndexProvider : IndexProvider<AIQACohort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIQACohortIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIQACohort> context)
    {
        context.For<AIQACohortIndex>()
            .Map(cohort => new AIQACohortIndex
            {
                ItemId = cohort.ItemId,
                Name = cohort.Name,
                Enabled = cohort.Enabled,
            });
    }
}

/// <summary>
/// Creates the QA cohort index table.
/// </summary>
public static class AIQACohortIndexSchema
{
    /// <summary>
    /// Creates the table and its index.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIQACohortIndex>(table => table
            .Column<string>(nameof(AIQACohortIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIQACohortIndex.Name), column => column.WithLength(255))
            .Column<bool>(nameof(AIQACohortIndex.Enabled)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIQACohortIndex>(
            table => table.CreateIndex("IDX_AIQACohort_ItemId", nameof(AIQACohortIndex.ItemId), "DocumentId"),
            collection: options.AICollectionName);
    }
}

/// <summary>
/// Finds AI alert rules by name and by whether they are checked.
/// </summary>
public sealed class AIAlertRuleIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule is checked.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Maps AI alert rules to their index.
/// </summary>
public sealed class AIAlertRuleIndexProvider : IndexProvider<AIAlertRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRuleIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIAlertRuleIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIAlertRule> context)
    {
        context.For<AIAlertRuleIndex>()
            .Map(rule => new AIAlertRuleIndex
            {
                ItemId = rule.ItemId,
                Name = rule.Name,
                Enabled = rule.Enabled,
            });
    }
}

/// <summary>
/// Creates the AI alert rule index table.
/// </summary>
public static class AIAlertRuleIndexSchema
{
    /// <summary>
    /// Creates the table and its index.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIAlertRuleIndex>(table => table
            .Column<string>(nameof(AIAlertRuleIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIAlertRuleIndex.Name), column => column.WithLength(255))
            .Column<bool>(nameof(AIAlertRuleIndex.Enabled)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIAlertRuleIndex>(
            table => table.CreateIndex("IDX_AIAlertRule_ItemId", nameof(AIAlertRuleIndex.ItemId), "DocumentId"),
            collection: options.AICollectionName);
    }
}
