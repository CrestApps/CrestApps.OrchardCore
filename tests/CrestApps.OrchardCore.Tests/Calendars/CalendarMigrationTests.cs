using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The calendar migrations make their index tables on a new site, and bring a version 1 site up to date, on SQLite with a
/// table prefix as a tenant has.
/// </summary>
public sealed class CalendarMigrationTests
{
    [Fact]
    public async Task NewSite_GetsEveryCalendarIndex()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            var calendars = await new CalendarMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            var reminders = new CalendarReminderMigrations { SchemaBuilder = schemaBuilder };
            var remindersCreated = await reminders.CreateAsync();
            var remindersUpgraded = await reminders.UpdateFrom1Async();

            Assert.Equal(3, calendars);
            Assert.Equal(1, remindersCreated);
            Assert.Equal(2, remindersUpgraded);
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarIndex), "IDX_CalendarIndex_FeedToken"));
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarEventIndex), "IDX_CalendarEventIndex_Range"));
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarEventAttendeeIndex), "IDX_CalendarEventAttendeeIndex_User"));
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarCategoryIndex), "IDX_CalendarCategoryIndex_OwnerId"));
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarReminderLogIndex), "IDX_CalendarReminderLogIndex_Key"));
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarUserReminderIndex), "IDX_CalendarUserReminderIndex_User"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Version1Site_GetsTheCategoriesIndexOnUpgrade()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            // Version 1 had no categories.
            await new CalendarMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await schemaBuilder.DropMapIndexTableAsync<CalendarCategoryIndex>(CalendarsConstants.CollectionName);

            Assert.False(await ExistsAsync(schemaBuilder, typeof(CalendarCategoryIndex), "IDX_CalendarCategoryIndex_OwnerId"));

            var version = await new CalendarMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.True(await ExistsAsync(schemaBuilder, typeof(CalendarCategoryIndex), "IDX_CalendarCategoryIndex_OwnerId"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Task<bool> ExistsAsync(ISchemaBuilder schemaBuilder, Type indexType, string indexName)
        => IndexTableIndexCreation.ExistsAsync(schemaBuilder, indexType, indexName, CalendarsConstants.CollectionName);

    private static string DatabasePath()
        => Path.Combine(Path.GetTempPath(), $"calendar-migrations-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix("tenant_"));

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(CalendarsConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }
}
