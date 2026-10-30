using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Creates the calendar index tables. Shared by the module's migration and the tests, so both build the same schema.
/// </summary>
public static class CalendarIndexSchema
{
    /// <summary>
    /// Creates the index tables and their SQL indexes in the <see cref="CalendarsConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = CalendarsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<CalendarIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Type", column => column.Nullable().WithLength(100))
            .Column<CalendarOwnerKind>("OwnerKind")
            .Column<string>("OwnerId", column => column.Nullable().WithLength(255))
            .Column<CalendarVisibility>("Visibility")
            .Column<string>("Source", column => column.Nullable().WithLength(100))
            .Column<string>("SourceId", column => column.Nullable().WithLength(255))
            .Column<string>("FeedToken", column => column.Nullable().WithLength(64))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CalendarIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CalendarIndex_Owner", "OwnerId", "Type");
            table.CreateIndex("IDX_CalendarIndex_Source", "Source", "SourceId");
            table.CreateIndex("IDX_CalendarIndex_FeedToken", "FeedToken");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CalendarShareIndex>(table => table
            .Column<string>("CalendarId", column => column.WithLength(26))
            .Column<CalendarPrincipalKind>("PrincipalKind")
            .Column<string>("PrincipalId", column => column.WithLength(255))
            .Column<CalendarAccessLevel>("Access"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CalendarShareIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarShareIndex_Principal", "PrincipalKind", "PrincipalId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CalendarEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CalendarId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.Nullable().WithLength(100))
            .Column<CalendarEventStatus>("Status")
            .Column<CalendarTransparency>("Transparency")
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("EndUtc")
            .Column<DateTime>("RangeEndUtc")
            .Column<bool>("IsRecurring")
            .Column<string>("SeriesId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("OriginalStartUtc", column => column.Nullable())
            .Column<string>("Source", column => column.Nullable().WithLength(100))
            .Column<string>("SourceId", column => column.Nullable().WithLength(255))
            .Column<string>("Uid", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CalendarEventIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarEventIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CalendarEventIndex_Range", "CalendarId", "StartUtc", "RangeEndUtc");
            table.CreateIndex("IDX_CalendarEventIndex_Series", "SeriesId");
            table.CreateIndex("IDX_CalendarEventIndex_Source", "Source", "SourceId");
            table.CreateIndex("IDX_CalendarEventIndex_Uid", "CalendarId", "Uid");
            table.CreateIndex("IDX_CalendarEventIndex_Start", "StartUtc", "RangeEndUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CalendarEventAttendeeIndex>(table => table
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<string>("CalendarId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("ResourceCalendarId", column => column.Nullable().WithLength(26))
            .Column<string>("Email", column => column.Nullable().WithLength(255))
            .Column<CalendarAttendeeResponse>("Response")
            .Column<CalendarEventStatus>("Status")
            .Column<DateTime>("StartUtc")
            .Column<DateTime>("RangeEndUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CalendarEventAttendeeIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarEventAttendeeIndex_User", "UserId", "StartUtc", "RangeEndUtc");
            table.CreateIndex("IDX_CalendarEventAttendeeIndex_Resource", "ResourceCalendarId", "StartUtc", "RangeEndUtc");
            table.CreateIndex("IDX_CalendarEventAttendeeIndex_Email", "Email");
        }, collection: collection);

        await CreateCategoriesAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the index of calendar categories.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateCategoriesAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<CalendarCategoryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OwnerId", column => column.Nullable().WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100)),
            collection: CalendarsConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<CalendarCategoryIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarCategoryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CalendarCategoryIndex_OwnerId", "OwnerId");
        }, collection: CalendarsConstants.CollectionName);
    }
}
