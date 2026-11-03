using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// An event's own reminders reach its people once each, on a real SQLite store.
/// </summary>
public sealed class CalendarUserRemindersTests
{
    // The harness clock says 12:00 UTC on 2 November 2026, 07:00 in New York.
    private static readonly DateTime _inTenMinutes = new(2026, 11, 2, 7, 10, 0);

    [Fact]
    public async Task Reminder_ReachesTheOwnerAndInvitedUsers_Once()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Mine", c => c.OwnerId = "owner");
        var created = await harness.CreateEventAsync(calendar.ItemId, _inTenMinutes, _inTenMinutes.AddMinutes(30), e =>
        {
            e.Title = "Call back Ada";
            e.Attendees.Add(new CalendarAttendee { UserId = "invited", Response = CalendarAttendeeResponse.Accepted });
            e.Attendees.Add(new CalendarAttendee { UserId = "declined", Response = CalendarAttendeeResponse.Declined });
        });

        // Act
        var first = await RunAsync(harness);
        var second = await RunAsync(harness);

        // Assert
        Assert.True(created.Succeeded);
        Assert.Equal([10], created.Value.ReminderMinutes);
        Assert.Equal(["invited", "owner"], (await ShownAsync(harness, "owner")).Concat(await ShownAsync(harness, "invited")).Select(reminder => reminder.UserId).Order());
        Assert.Empty(await ShownAsync(harness, "declined"));
        Assert.True(first > 0);
        Assert.Equal(0, second);

        var reminder = Assert.Single(await ShownAsync(harness, "owner"));

        Assert.Equal("Call back Ada", reminder.Title);
        Assert.Equal(10, reminder.MinutesBefore);
        Assert.Equal(new DateTime(2026, 11, 2, 12, 10, 0, DateTimeKind.Utc), reminder.StartUtc);
    }

    [Fact]
    public async Task EventWithoutReminder_RemindsNobody()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Mine", c => c.OwnerId = "owner");

        await harness.CreateEventAsync(calendar.ItemId, _inTenMinutes, _inTenMinutes.AddMinutes(30), e => e.ReminderMinutes = []);

        // Act
        await RunAsync(harness);

        // Assert
        Assert.Empty(await ShownAsync(harness, "owner"));
    }

    [Fact]
    public async Task DismissedReminder_IsNoLongerShown()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Mine", c => c.OwnerId = "owner");

        await harness.CreateEventAsync(calendar.ItemId, _inTenMinutes, _inTenMinutes.AddMinutes(30));
        await RunAsync(harness);

        var reminder = Assert.Single(await ShownAsync(harness, "owner"));

        // Act
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CalendarUserReminderStore>();

            await store.DismissAsync(await store.FindAsync(reminder.ItemId, TestContext.Current.CancellationToken), CalendarTestHarness.Now, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Empty(await ShownAsync(harness, "owner"));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<CalendarReminderService>();
                services.AddScoped<CalendarUserReminderStore>();
                services.AddScoped<ICalendarReminderObserver, CalendarUserReminderObserver>();
            },
            createSchema: async schemaBuilder =>
            {
                await schemaBuilder.CreateMapIndexTableAsync<CalendarReminderLogIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("Key", column => column.WithLength(255))
                    .Column<DateTime>("RaisedUtc"),
                    collection: CalendarsConstants.CollectionName);

                await schemaBuilder.CreateMapIndexTableAsync<CalendarUserReminderIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("UserId", column => column.WithLength(26))
                    .Column<bool>("Dismissed")
                    .Column<DateTime>("CreatedUtc"),
                    collection: CalendarsConstants.CollectionName);
            },
            indexProviders: [new CalendarReminderLogIndexProvider(), new CalendarUserReminderIndexProvider()]);
    }

    private static Task<int> RunAsync(CalendarTestHarness harness)
    {
        return harness.RunAsync(services => services.GetRequiredService<CalendarReminderService>().RunAsync(TestContext.Current.CancellationToken));
    }

    private static Task<IReadOnlyList<CrestApps.OrchardCore.Calendars.Core.Models.CalendarUserReminder>> ShownAsync(CalendarTestHarness harness, string userId)
    {
        return harness.RunAsync(
            services => services.GetRequiredService<CalendarUserReminderStore>().ListShownAsync(userId, CalendarTestHarness.Now.AddDays(-1), 20, TestContext.Current.CancellationToken),
            commit: false);
    }
}
