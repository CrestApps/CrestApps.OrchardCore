using CrestApps.OrchardCore.Calendars.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Calendars.Migrations;

internal sealed class CalendarReminderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CalendarReminderLogIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Key", column => column.WithLength(255))
            .Column<DateTime>("RaisedUtc"),
            collection: CalendarsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CalendarReminderLogIndex>(table =>
        {
            table.CreateIndex("IDX_CalendarReminderLogIndex_Key", "Key");
            table.CreateIndex("IDX_CalendarReminderLogIndex_RaisedUtc", "RaisedUtc");
        }, collection: CalendarsConstants.CollectionName);

        return 1;
    }
}
