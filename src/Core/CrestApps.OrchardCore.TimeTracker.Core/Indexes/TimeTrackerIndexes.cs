using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.TimeTracker.Core.Indexes;

/// <summary>
/// Indexes time categories by name.
/// </summary>
public sealed class TimeCategoryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }
}

/// <summary>
/// Indexes time stages by name.
/// </summary>
public sealed class TimeStageIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }
}

/// <summary>
/// Indexes time entries by person and time.
/// </summary>
public sealed class TimeEntryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the person's user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the stage.
    /// </summary>
    public string StageId { get; set; }

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end, in UTC; the far future while open, so range queries find open entries.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the person is still in it.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets where it came from.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what in the source it stands for.
    /// </summary>
    public string SourceRef { get; set; }
}

/// <summary>
/// Maps time records to their indexes.
/// </summary>
public sealed class TimeTrackerIndexProvider : IndexProvider<TimeEntry>
{
    /// <summary>
    /// The end an open entry is indexed with.
    /// </summary>
    public static readonly DateTime OpenEnd = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeTrackerIndexProvider"/> class.
    /// </summary>
    public TimeTrackerIndexProvider()
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TimeEntry> context)
    {
        context.For<TimeEntryIndex>()
            .Map(entry => new TimeEntryIndex
            {
                ItemId = entry.ItemId,
                UserId = entry.UserId,
                UserName = entry.UserName,
                StageId = entry.StageId,
                StartUtc = entry.StartUtc,
                EndUtc = entry.EndUtc ?? OpenEnd,
                IsOpen = entry.EndUtc is null,
                Source = entry.Source,
                SourceRef = entry.SourceRef,
            });
    }
}

/// <summary>
/// Maps time categories to their index.
/// </summary>
public sealed class TimeCategoryIndexProvider : IndexProvider<TimeCategory>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeCategoryIndexProvider"/> class.
    /// </summary>
    public TimeCategoryIndexProvider()
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TimeCategory> context)
        => context.For<TimeCategoryIndex>().Map(item => new TimeCategoryIndex { ItemId = item.ItemId, Name = item.Name, Code = item.Code });
}

/// <summary>
/// Maps time stages to their index.
/// </summary>
public sealed class TimeStageIndexProvider : IndexProvider<TimeStage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeStageIndexProvider"/> class.
    /// </summary>
    public TimeStageIndexProvider()
    {
        CollectionName = TimeTrackerConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TimeStage> context)
        => context.For<TimeStageIndex>().Map(item => new TimeStageIndex { ItemId = item.ItemId, Name = item.Name, Code = item.Code });
}

/// <summary>
/// Creates the time tracker index tables.
/// </summary>
public static class TimeTrackerIndexSchema
{
    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = TimeTrackerConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<TimeEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("UserName", column => column.Nullable().WithLength(255))
            .Column<string>("StageId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc")
            .Column<bool>("IsOpen")
            .Column<string>("Source", column => column.Nullable().WithLength(32))
            .Column<string>("SourceRef", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeEntryIndex>(table =>
        {
            table.CreateIndex("IDX_TimeEntryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TimeEntryIndex_User", "UserId", "StartUtc", "EndUtc");
            table.CreateIndex("IDX_TimeEntryIndex_Open", "IsOpen", "UserId");
            table.CreateIndex("IDX_TimeEntryIndex_Start", "StartUtc", "EndUtc");
            table.CreateIndex("IDX_TimeEntryIndex_Source", "Source", "SourceRef");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeCategoryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Code", column => column.Nullable().WithLength(32)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeCategoryIndex>(table => table.CreateIndex("IDX_TimeCategoryIndex_ItemId", "ItemId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<TimeStageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Code", column => column.Nullable().WithLength(32)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TimeStageIndex>(table => table.CreateIndex("IDX_TimeStageIndex_ItemId", "ItemId"), collection: collection);
    }
}
