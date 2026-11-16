using System.Data.Common;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Core.Handlers;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Legacy;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The Contact Center's business hours calendars move to the Business Hours module: each stored calendar becomes a
/// schedule with the same id, so the queues, entry points, dialing profiles and batches that name it keep their hours;
/// running the move again changes nothing, and the old recipe step still imports calendars, as schedules.
/// </summary>
public sealed class BusinessHoursCalendarMoveTests
{
    private const string SupportId = "4spprt00000000000000000000";
    private const string NightsId = "4nghts00000000000000000000";
    private const string ClosedId = "4cxsed00000000000000000000";

    [Fact]
    public async Task TheMove_CopiesEachCalendar_ToAScheduleWithTheSameId()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var created = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        await InsertStoredCalendarsAsync(harness.Store,
            new StoredCalendar
            {
                ItemId = SupportId,
                Name = "Support",
                Description = "Phones and chat.",
                TimeZoneId = "America/New_York",
                Enabled = true,
                CreatedUtc = created,
                ModifiedUtc = created.AddDays(2),
                WeeklySchedule =
                [
                    new StoredDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 540, CloseMinute = 1020 },
                    new StoredDay { Day = DayOfWeek.Tuesday, IsOpen = true, OpenMinute = 540, CloseMinute = 1020 },

                    // A day turned off keeps its times in the old editor; it is closed.
                    new StoredDay { Day = DayOfWeek.Saturday, IsOpen = false, OpenMinute = 600, CloseMinute = 840 },
                ],
                Holidays = [new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 25), new DateOnly(2027, 1, 1)],
            },
            new StoredCalendar
            {
                // No time zone means UTC, and must not become the site's time zone on the way.
                ItemId = NightsId,
                Name = "Nights",
                Enabled = true,
                WeeklySchedule = [new StoredDay { Day = DayOfWeek.Friday, IsOpen = true, OpenMinute = 1320, CloseMinute = 120 }],
            },
            new StoredCalendar
            {
                ItemId = ClosedId,
                Name = "Retired",
                Enabled = false,
                WeeklySchedule = [new StoredDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 0, CloseMinute = 0 }],
            });

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<BusinessHoursCalendarMove>().MoveAsync(TestContext.Current.CancellationToken));
        var schedules = await ListSchedulesAsync(harness);

        // Assert
        Assert.Equal(3, moved);
        Assert.Equal(3, schedules.Count);

        var support = schedules.Single(schedule => schedule.ItemId == SupportId);

        Assert.Equal("Support", support.Name);
        Assert.Equal("Phones and chat.", support.Description);
        Assert.True(support.Enabled);
        Assert.Equal(created, support.CreatedUtc);
        Assert.Equal("America/New_York", support.Hours.TimeZoneId);
        Assert.Equal(
            [(DayOfWeek.Monday, 540, 1020), (DayOfWeek.Tuesday, 540, 1020)],
            support.Hours.Windows.Select(window => (window.Day, window.OpenMinute, window.CloseMinute)));
        Assert.Equal([new DateOnly(2026, 12, 25), new DateOnly(2027, 1, 1)], support.Hours.DateExceptions.Select(exception => exception.Date));
        Assert.All(support.Hours.DateExceptions, exception => Assert.True(exception.Closed));

        var nights = schedules.Single(schedule => schedule.ItemId == NightsId);

        Assert.Null(nights.Hours.TimeZoneId);
        Assert.Equal((DayOfWeek.Friday, 1320, 120), (nights.Hours.Windows[0].Day, nights.Hours.Windows[0].OpenMinute, nights.Hours.Windows[0].CloseMinute));

        var retired = schedules.Single(schedule => schedule.ItemId == ClosedId);

        Assert.False(retired.Enabled);
        Assert.Equal((0, 0), (retired.Hours.Windows[0].OpenMinute, retired.Hours.Windows[0].CloseMinute));
    }

    [Fact]
    public async Task TheMove_KeepsAScheduleThatHasTheSameName_AndNumbersTheCopy()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();

        var existingId = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            schedule.Name = "Support";
            await manager.CreateAsync(schedule, TestContext.Current.CancellationToken);

            return schedule.ItemId;
        });

        await InsertStoredCalendarsAsync(harness.Store,
            new StoredCalendar { ItemId = SupportId, Name = "Support", Enabled = true },
            new StoredCalendar { ItemId = NightsId, Name = "support", Enabled = true });

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<BusinessHoursCalendarMove>().MoveAsync(TestContext.Current.CancellationToken));
        var schedules = await ListSchedulesAsync(harness);

        // Assert
        Assert.Equal(2, moved);
        Assert.Equal("Support", schedules.Single(schedule => schedule.ItemId == existingId).Name);
        Assert.Equal("Support (2)", schedules.Single(schedule => schedule.ItemId == SupportId).Name);
        Assert.Equal("support (3)", schedules.Single(schedule => schedule.ItemId == NightsId).Name);
    }

    [Fact]
    public async Task TheMove_RunTwice_CopiesNothingMore_AndLeavesAnEditedScheduleAlone()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();

        await InsertStoredCalendarsAsync(harness.Store, new StoredCalendar
        {
            ItemId = SupportId,
            Name = "Support",
            Enabled = true,
            WeeklySchedule = [new StoredDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 540, CloseMinute = 1020 }],
        });

        await harness.RunAsync(services => services.GetRequiredService<BusinessHoursCalendarMove>().MoveAsync(TestContext.Current.CancellationToken));

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await manager.FindByIdAsync(SupportId, TestContext.Current.CancellationToken);

            schedule.Name = "Support desk";
            await manager.UpdateAsync(schedule, cancellationToken: TestContext.Current.CancellationToken);
        });

        // Act
        var second = await harness.RunAsync(services => services.GetRequiredService<BusinessHoursCalendarMove>().MoveAsync(TestContext.Current.CancellationToken));
        var schedule = Assert.Single(await ListSchedulesAsync(harness));

        // Assert
        Assert.Equal(0, second);
        Assert.Equal("Support desk", schedule.Name);
    }

    [Fact]
    public async Task TheMove_WithNoCalendars_DoesNothing()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<BusinessHoursCalendarMove>().MoveAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, moved);
        Assert.Empty(await ListSchedulesAsync(harness));
    }

    [Fact]
    public async Task TheOldRecipeStep_ImportsCalendars_AsSchedules()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();

        var existingId = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            schedule.Name = "Weekend";
            await manager.CreateAsync(schedule, TestContext.Current.CancellationToken);

            return schedule.ItemId;
        });

        var step = new JsonObject
        {
            ["name"] = "ContactCenterBusinessHoursCalendar",
            ["Calendars"] = new JsonArray
            {
                new JsonObject
                {
                    ["ItemId"] = SupportId,
                    ["Name"] = "Support",
                    ["TimeZoneId"] = "Europe/London",
                    ["Enabled"] = true,
                    ["WeeklySchedule"] = new JsonArray
                    {
                        new JsonObject { ["Day"] = "Monday", ["IsOpen"] = true, ["OpenMinute"] = 480, ["CloseMinute"] = 960 },
                        new JsonObject { ["Day"] = 2, ["IsOpen"] = true, ["OpenMinute"] = 480, ["CloseMinute"] = 960 },
                    },
                    ["Holidays"] = new JsonArray { "2026-12-25" },
                },

                // No id: matched to the schedule of the same name, which takes the calendar's hours.
                new JsonObject
                {
                    ["Name"] = "Weekend",
                    ["Enabled"] = true,
                    ["WeeklySchedule"] = new JsonArray
                    {
                        new JsonObject { ["Day"] = "Saturday", ["IsOpen"] = true, ["OpenMinute"] = 600, ["CloseMinute"] = 840 },
                    },
                },

                // Refused like the editor refuses it, and the others are still imported.
                new JsonObject { ["ItemId"] = NightsId, ["Name"] = " " },
            },
        };

        var context = new RecipeExecutionContext { Name = "ContactCenterBusinessHoursCalendar", Step = step };

        // Act
        await harness.RunAsync(services => services.GetRequiredService<ContactCenterBusinessHoursCalendarStep>().ExecuteAsync(context));
        var schedules = await ListSchedulesAsync(harness);

        // Assert
        Assert.Equal(2, schedules.Count);

        var support = schedules.Single(schedule => schedule.ItemId == SupportId);

        Assert.Equal("Europe/London", support.Hours.TimeZoneId);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], support.Hours.Windows.Select(window => window.Day));
        Assert.Equal(new DateOnly(2026, 12, 25), Assert.Single(support.Hours.DateExceptions).Date);

        var weekend = schedules.Single(schedule => schedule.ItemId == existingId);

        Assert.Equal((DayOfWeek.Saturday, 600, 840), (weekend.Hours.Windows[0].Day, weekend.Hours.Windows[0].OpenMinute, weekend.Hours.Windows[0].CloseMinute));
        Assert.Single(context.Errors);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddCatalogs();
                services.AddCatalogManagers();
                services.AddBusinessHoursSchedules();
                services.AddScoped<ICatalogEntryHandler<BusinessHoursSchedule>, BusinessHoursScheduleHandler>();
                services.AddScoped<BusinessHoursCalendarMove>();
                services.AddScoped<ContactCenterBusinessHoursCalendarStep>();
            },
            collections: [ContactCenterStorage.CollectionName]);

    private static Task<List<BusinessHoursSchedule>> ListSchedulesAsync(CalendarTestHarness harness)
        => harness.RunAsync(
            async services => (await services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>().GetAllAsync(TestContext.Current.CancellationToken)).ToList(),
            commit: false);

    // Writes calendars the way the Contact Center stored them: one document each in its collection, under the old type
    // name, which no longer exists.
    private static async Task InsertStoredCalendarsAsync(IStore store, params StoredCalendar[] calendars)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var id = 100_000;

        foreach (var calendar in calendars)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "INSERT INTO ContactCenter_Document (Id, Type, Content, Version) VALUES (@id, @type, @content, 0)";
            AddParameter(command, "@id", id++);
            AddParameter(command, "@type", BusinessHoursCalendarMove.StoredTypeName);
            AddParameter(command, "@content", store.Configuration.ContentSerializer.Serialize(calendar));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // The shape the Contact Center stored its calendars in.
    private sealed class StoredCalendar : CatalogItem
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string TimeZoneId { get; set; }

        public IList<StoredDay> WeeklySchedule { get; set; } = [];

        public IList<DateOnly> Holidays { get; set; } = [];

        public bool Enabled { get; set; } = true;

        public DateTime CreatedUtc { get; set; }

        public DateTime? ModifiedUtc { get; set; }
    }

    private sealed class StoredDay
    {
        public DayOfWeek Day { get; set; }

        public bool IsOpen { get; set; }

        public int OpenMinute { get; set; }

        public int CloseMinute { get; set; }
    }
}
