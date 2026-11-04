using System.Reflection;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Invitations;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Doing the same thing twice (a retried request, a link opened twice, a background run that overlaps, a feed imported
/// again) has the effect of doing it once, and one record's or one tenant's codes never open another's.
/// </summary>
public sealed class CalendarRetryAndIsolationTests
{
    // The harness clock says 12:00 UTC on 2 November 2026, 07:00 in New York.
    private static readonly DateTime _inTenMinutes = new(2026, 11, 2, 7, 10, 0);
    private static readonly DateTime _start = new(2026, 11, 4, 10, 0, 0);

    [Fact]
    public async Task CancellingTwice_RaisesOneCancellation()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1));

        // Act
        var first = await CancelAsync(harness, created.Value.ItemId);
        var second = await CancelAsync(harness, created.Value.ItemId);

        // Assert
        Assert.True(first.Result.Succeeded);
        Assert.True(second.Result.Succeeded);
        Assert.Equal([CalendarEventChangeKind.Cancelled], first.Pending.Select(change => change.Kind));
        Assert.Empty(second.Pending);
    }

    [Fact]
    public async Task TheSameAnswerTwice_RaisesOneResponse()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var created = await harness.CreateEventAsync(calendar.ItemId, _start, _start.AddHours(1), e => e.Attendees.Add(new CalendarAttendee { Email = "jane@contoso.com", Name = "Jane" }));

        // Act
        var first = await RespondAsync(harness, created.Value.ItemId, CalendarAttendeeResponse.Accepted);
        var again = await RespondAsync(harness, created.Value.ItemId, CalendarAttendeeResponse.Accepted);
        var changed = await RespondAsync(harness, created.Value.ItemId, CalendarAttendeeResponse.Declined);

        // Assert
        Assert.Contains(CalendarEventChangeKind.ResponseChanged, first.Select(change => change.Kind));
        Assert.Empty(again);
        Assert.Contains(CalendarEventChangeKind.ResponseChanged, changed.Select(change => change.Kind));
    }

    [Fact]
    public async Task Reminders_ReachTheObserversOnce_AcrossRunsAndAPurgeOfOldLogs()
    {
        // Arrange
        var observer = new CountingReminderObserver();
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<CalendarReminderService>();
                services.AddSingleton<ICalendarReminderObserver>(observer);
            },
            createSchema: async schemaBuilder =>
            {
                // The module's own migrations make the reminder log, as a site upgrading through them would have it.
                var migrations = new CalendarReminderMigrations { SchemaBuilder = schemaBuilder };

                Assert.Equal(1, await migrations.CreateAsync());
                Assert.Equal(2, await migrations.UpdateFrom1Async());
            },
            indexProviders: [new CalendarReminderLogIndexProvider(), new CalendarUserReminderIndexProvider()]);
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Mine", c => c.OwnerId = "owner");

        await harness.CreateEventAsync(calendar.ItemId, _inTenMinutes, _inTenMinutes.AddMinutes(30), e => e.ReminderMinutes = [10]);

        // Act
        var first = await RunRemindersAsync(harness);
        var second = await RunRemindersAsync(harness);
        var purged = await harness.RunAsync(services => services.GetRequiredService<CalendarReminderService>().PurgeAsync(CalendarTestHarness.Now.AddDays(-7)));
        var third = await RunRemindersAsync(harness);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(0, purged);
        Assert.Equal(0, third);
        Assert.Equal(1, observer.StartingSoon);
    }

    [Theory]
    [InlineData(typeof(CalendarReminderBackgroundTask))]
    [InlineData(typeof(CrestApps.OrchardCore.Scheduling.BackgroundTasks.AppointmentHoldsBackgroundTask))]
    [InlineData(typeof(CrestApps.OrchardCore.Bookings.BackgroundTasks.BookingsBackgroundTask))]
    public void BackgroundTasks_TakeATenantLock_SoTwoRunsNeverOverlap(Type taskType)
    {
        // The reminder log is read and then written in one run; two runs at once on two app instances could both miss the
        // other's log row. The background task lock is what keeps a tenant's runs one at a time.
        var attribute = taskType.GetCustomAttribute<BackgroundTaskAttribute>();

        Assert.NotNull(attribute);
        Assert.True(attribute.LockTimeout > 0);
        Assert.True(attribute.LockExpiration > 0);
    }

    [Fact]
    public async Task ImportingTheSameFeedTwice_AddsItsEventsOnce()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(services => services.AddScoped<ICalendarImporter>());
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Imported", c => c.TimeZoneId = "UTC");
        var feed = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Contoso//Test//EN",
            "BEGIN:VEVENT",
            "UID:standup@contoso.com",
            "LAST-MODIFIED:20261101T000000Z",
            "DTSTART:20261104T150000Z",
            "DTEND:20261104T151500Z",
            "RRULE:FREQ=DAILY;COUNT=5",
            "SUMMARY:Stand-up",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:standup@contoso.com",
            "RECURRENCE-ID:20261105T150000Z",
            "DTSTART:20261105T160000Z",
            "DTEND:20261105T161500Z",
            "SUMMARY:Stand-up, later",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:review@contoso.com",
            "LAST-MODIFIED:20261101T000000Z",
            "DTSTART:20261106T150000Z",
            "DTEND:20261106T160000Z",
            "SUMMARY:Review",
            "END:VEVENT",
            "END:VCALENDAR",
            string.Empty);

        // Act
        var first = await ImportAsync(harness, calendar.ItemId, feed);
        var second = await ImportAsync(harness, calendar.ItemId, feed);
        var stored = await harness.RunAsync(services => services.GetRequiredService<ICalendarEventStore>().ListByCalendarAsync(calendar.ItemId, 0, 100), commit: false);

        // Assert
        Assert.Empty(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(3, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Equal(3, stored.Count);
        Assert.Single(stored, item => item.Uid == "review@contoso.com");
        Assert.Single(stored, item => item.Uid == "standup@contoso.com" && item.SeriesId is null);
        Assert.Single(stored, item => item.SeriesId is not null);
    }

    [Fact]
    public async Task AFeedToken_OpensOnlyItsOwnCalendar_AndNothingInAnotherTenant()
    {
        // Arrange (each harness is its own database, as each tenant has its own)
        await using var tenantA = await CalendarTestHarness.CreateAsync();
        await using var tenantB = await CalendarTestHarness.CreateAsync();
        var tokenA = new string('a', 48);
        var tokenB = new string('b', 48);
        var first = await tenantA.CreateCalendarAsync(CalendarTestHarness.General, "First", c => c.FeedToken = tokenA);
        var second = await tenantA.CreateCalendarAsync(CalendarTestHarness.General, "Second", c => c.FeedToken = tokenB);

        await tenantB.CreateCalendarAsync(CalendarTestHarness.General, "Other tenant", c => c.FeedToken = new string('c', 48));

        // Act
        var byA = await FindByFeedTokenAsync(tenantA, tokenA);
        var byB = await FindByFeedTokenAsync(tenantA, tokenB);
        var byPrefix = await FindByFeedTokenAsync(tenantA, tokenA[..40]);
        var inOtherTenant = await FindByFeedTokenAsync(tenantB, tokenA);

        // Assert
        Assert.Equal(first.ItemId, byA?.ItemId);
        Assert.Equal(second.ItemId, byB?.ItemId);
        Assert.Null(byPrefix);
        Assert.Null(inOtherTenant);
    }

    [Fact]
    public void AnInvitationToken_NamesOnlyItsOwnAttendeeOfItsOwnEvent()
    {
        // Arrange
        var jane = new CalendarAttendee { Email = "jane@contoso.com", ResponseToken = "token-jane-0123456789" };
        var john = new CalendarAttendee { Email = "john@contoso.com", ResponseToken = "token-john-0123456789" };
        var planning = new CalendarEvent { ItemId = "planning", Attendees = [jane, john] };
        var review = new CalendarEvent { ItemId = "review", Attendees = [new CalendarAttendee { Email = "ann@contoso.com", ResponseToken = "token-ann-0123456789" }] };

        // Act & Assert
        Assert.Same(jane, CalendarResponseEndpoint.FindAttendee(planning, "token-jane-0123456789"));
        Assert.Same(john, CalendarResponseEndpoint.FindAttendee(planning, "token-john-0123456789"));
        Assert.Null(CalendarResponseEndpoint.FindAttendee(review, "token-jane-0123456789"));
        Assert.Null(CalendarResponseEndpoint.FindAttendee(planning, "token-jane-012345678"));
        Assert.Null(CalendarResponseEndpoint.FindAttendee(planning, string.Empty));
        Assert.Null(CalendarResponseEndpoint.FindAttendee(new CalendarEvent { Attendees = [new CalendarAttendee { Email = "no-token@contoso.com" }] }, null));
    }

    private static Task<(CalendarResult<CalendarEvent> Result, List<CalendarEventChange> Pending)> CancelAsync(CalendarTestHarness harness, string eventId)
    {
        return harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var result = await events.CancelAsync(await events.FindByIdAsync(eventId));

            return (result, services.GetRequiredService<CalendarChangeQueue>().Pending.ToList());
        });
    }

    private static Task<List<CalendarEventChange>> RespondAsync(CalendarTestHarness harness, string eventId, CalendarAttendeeResponse response)
    {
        return harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var result = await events.RespondAsync(await events.FindByIdAsync(eventId), "email:jane@contoso.com", response);

            Assert.True(result.Succeeded);

            return services.GetRequiredService<CalendarChangeQueue>().Pending.ToList();
        });
    }

    private static Task<int> RunRemindersAsync(CalendarTestHarness harness)
    {
        return harness.RunAsync(services => services.GetRequiredService<CalendarReminderService>().RunAsync(TestContext.Current.CancellationToken));
    }

    private static Task<ICalendarImportResult> ImportAsync(CalendarTestHarness harness, string calendarId, string feed)
    {
        return harness.RunAsync(async services =>
        {
            var calendar = await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarId);

            return await services.GetRequiredService<ICalendarImporter>().ImportAsync(calendar, ICalendarReader.Read(feed, calendar.TimeZoneId), new ICalendarImportOptions { Actor = "test" });
        });
    }

    private static Task<Calendar> FindByFeedTokenAsync(CalendarTestHarness harness, string token)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICalendarStore>().FindByFeedTokenAsync(token), commit: false);
    }

    private sealed class CountingReminderObserver : ICalendarReminderObserver
    {
        private int _startingSoon;

        public int StartingSoon => _startingSoon;

        public Task OnStartingSoonAsync(CalendarReminderContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _startingSoon);

            return Task.CompletedTask;
        }

        public Task OnCompletedAsync(CalendarReminderContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
