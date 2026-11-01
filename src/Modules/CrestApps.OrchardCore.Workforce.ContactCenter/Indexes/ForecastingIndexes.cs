using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;

/// <summary>
/// Indexes planning groups by name and team.
/// </summary>
public sealed class PlanningGroupIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }
}

/// <summary>
/// Indexes forecast events by name and first day.
/// </summary>
public sealed class ForecastEventIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the first day, at midnight.
    /// </summary>
    public DateTime StartDate { get; set; }
}

/// <summary>
/// Indexes forecasts by planning group, status and range.
/// </summary>
public sealed class ForecastIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the first day, at midnight.
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Gets or sets the last day, at midnight.
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps planning groups.
/// </summary>
public sealed class PlanningGroupIndexProvider : IndexProvider<PlanningGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupIndexProvider"/> class.
    /// </summary>
    public PlanningGroupIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<PlanningGroup> context)
    {
        context.For<PlanningGroupIndex>()
            .Map(item => new PlanningGroupIndex { ItemId = item.ItemId, Name = item.Name, TeamId = item.TeamId });
    }
}

/// <summary>
/// Maps forecast events.
/// </summary>
public sealed class ForecastEventIndexProvider : IndexProvider<ForecastEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastEventIndexProvider"/> class.
    /// </summary>
    public ForecastEventIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ForecastEvent> context)
    {
        context.For<ForecastEventIndex>()
            .Map(item => new ForecastEventIndex { ItemId = item.ItemId, Name = item.Name, StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
    }
}

/// <summary>
/// Maps forecasts.
/// </summary>
public sealed class ForecastIndexProvider : IndexProvider<Forecast>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastIndexProvider"/> class.
    /// </summary>
    public ForecastIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Forecast> context)
    {
        context.For<ForecastIndex>()
            .Map(item => new ForecastIndex
            {
                ItemId = item.ItemId,
                PlanningGroupId = item.PlanningGroupId,
                Status = item.Status.ToString(),
                FromDate = item.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                ToDate = item.ToDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                CreatedUtc = item.CreatedUtc,
            });
    }
}

/// <summary>
/// Creates the forecasting index tables.
/// </summary>
public static class ForecastingIndexSchema
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

        await schemaBuilder.CreateMapIndexTableAsync<PlanningGroupIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PlanningGroupIndex>(table =>
        {
            table.CreateIndex("IDX_PlanningGroupIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PlanningGroupIndex_Team", "TeamId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ForecastEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<DateTime>("StartDate"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ForecastEventIndex>(table =>
        {
            table.CreateIndex("IDX_ForecastEventIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ForecastEventIndex_Start", "StartDate");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<ForecastIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PlanningGroupId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<DateTime>("FromDate")
            .Column<DateTime>("ToDate")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ForecastIndex>(table =>
        {
            table.CreateIndex("IDX_ForecastIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ForecastIndex_Group", "PlanningGroupId", "Status", "FromDate");
        }, collection: collection);
    }
}
