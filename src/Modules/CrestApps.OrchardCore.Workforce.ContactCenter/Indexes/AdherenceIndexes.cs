using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;

/// <summary>
/// Indexes adherence rule sets by name.
/// </summary>
public sealed class AdherenceRuleSetIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// Indexes adherence exceptions by worker, team and time.
/// </summary>
public sealed class AdherenceExceptionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }
}

/// <summary>
/// Indexes adherence days by worker, team and day.
/// </summary>
public sealed class AdherenceDayIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the day, at midnight.
    /// </summary>
    public DateTime Date { get; set; }
}

/// <summary>
/// Indexes adherence alerts by episode key.
/// </summary>
public sealed class AdherenceAlertIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the episode key.
    /// </summary>
    public string Key { get; set; }
}

/// <summary>
/// Maps adherence rule sets.
/// </summary>
public sealed class AdherenceRuleSetIndexProvider : IndexProvider<AdherenceRuleSet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetIndexProvider"/> class.
    /// </summary>
    public AdherenceRuleSetIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AdherenceRuleSet> context)
    {
        context.For<AdherenceRuleSetIndex>()
            .Map(item => new AdherenceRuleSetIndex { ItemId = item.ItemId, Name = item.Name });
    }
}

/// <summary>
/// Maps adherence exceptions.
/// </summary>
public sealed class AdherenceExceptionIndexProvider : IndexProvider<AdherenceException>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceExceptionIndexProvider"/> class.
    /// </summary>
    public AdherenceExceptionIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AdherenceException> context)
    {
        context.For<AdherenceExceptionIndex>()
            .Map(item => new AdherenceExceptionIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                StartUtc = item.StartUtc,
                EndUtc = item.EndUtc,
            });
    }
}

/// <summary>
/// Maps adherence days.
/// </summary>
public sealed class AdherenceDayIndexProvider : IndexProvider<AdherenceDay>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceDayIndexProvider"/> class.
    /// </summary>
    public AdherenceDayIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AdherenceDay> context)
    {
        context.For<AdherenceDayIndex>()
            .Map(item => new AdherenceDayIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                TeamId = item.TeamId,
                Date = item.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            });
    }
}

/// <summary>
/// Maps adherence alerts.
/// </summary>
public sealed class AdherenceAlertIndexProvider : IndexProvider<AdherenceAlert>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceAlertIndexProvider"/> class.
    /// </summary>
    public AdherenceAlertIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AdherenceAlert> context)
    {
        context.For<AdherenceAlertIndex>()
            .Map(item => new AdherenceAlertIndex { ItemId = item.ItemId, Key = item.Key });
    }
}

/// <summary>
/// Creates the adherence index tables.
/// </summary>
public static class AdherenceIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<AdherenceRuleSetIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AdherenceRuleSetIndex>(table =>
        {
            table.CreateIndex("IDX_AdherenceRuleSetIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AdherenceRuleSetIndex_Name", "Name");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AdherenceExceptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AdherenceExceptionIndex>(table =>
        {
            table.CreateIndex("IDX_AdherenceExceptionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AdherenceExceptionIndex_Worker", "WorkerId", "StartUtc", "EndUtc");
            table.CreateIndex("IDX_AdherenceExceptionIndex_Team", "TeamId", "StartUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AdherenceDayIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("WorkerId", column => column.Nullable().WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("Date"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AdherenceDayIndex>(table =>
        {
            table.CreateIndex("IDX_AdherenceDayIndex_Worker", "WorkerId", "Date");
            table.CreateIndex("IDX_AdherenceDayIndex_Team", "TeamId", "Date");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AdherenceAlertIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Key", column => column.Nullable().WithLength(200)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AdherenceAlertIndex>(table =>
        {
            table.CreateIndex("IDX_AdherenceAlertIndex_Key", "Key");
        }, collection: collection);
    }
}
