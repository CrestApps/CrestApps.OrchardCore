using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// A follow-up activity placed on its owner's calendar, and the activity and its event kept at the same time whichever one
/// moves, with the real calendar services on SQLite.
/// </summary>
public sealed class ActivityCalendarSyncTests
{
    private static readonly DateTime _tomorrowAtTen = new(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Place_PutsTheActivityOnTheOwnersPersonalCalendar()
    {
        // Arrange
        var activities = new ActivityRepository();
        await using var harness = await CreateAsync(activities);
        var activity = activities.Add(NewActivity());

        // Act
        var placed = await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().PlaceAsync(activity, "agent-1", "agent", TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(placed);
        Assert.Equal("Call Ada Lovelace", placed.Title);
        Assert.Equal(_tomorrowAtTen, placed.StartUtc);
        Assert.Equal(_tomorrowAtTen.AddMinutes(ActivityCalendarService.DurationMinutes), placed.EndUtc);
        Assert.Equal(ActivityCalendarService.Source, placed.Source);
        Assert.Equal(activity.ItemId, placed.SourceId);
        Assert.True(activity.TryGet<ActivityCalendarLink>(out var link));
        Assert.Equal(placed.ItemId, link.EventId);

        var calendar = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().FindByIdAsync(placed.CalendarId).AsTask(), commit: false);

        Assert.Equal(CalendarOwnerKind.User, calendar.OwnerKind);
        Assert.Equal("agent-1", calendar.OwnerId);
    }

    [Fact]
    public async Task ReschedulingTheActivity_MovesTheEvent_AndCancellingItCancelsTheEvent()
    {
        // Arrange
        var activities = new ActivityRepository();
        await using var harness = await CreateAsync(activities);
        var activity = activities.Add(NewActivity());
        var placed = await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().PlaceAsync(activity, "agent-1", "agent", TestContext.Current.CancellationToken));

        // Act
        activity.ScheduledUtc = _tomorrowAtTen.AddHours(2);
        await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().SyncEventAsync(activity, TestContext.Current.CancellationToken));
        var moved = await FindEventAsync(harness, placed.ItemId);

        activity.Status = ActivityStatus.Cancelled;
        await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().SyncEventAsync(activity, TestContext.Current.CancellationToken));
        var cancelled = await FindEventAsync(harness, placed.ItemId);

        // Assert
        Assert.Equal(_tomorrowAtTen.AddHours(2), moved.StartUtc);
        Assert.Equal(_tomorrowAtTen.AddHours(2).AddMinutes(ActivityCalendarService.DurationMinutes), moved.EndUtc);
        Assert.Equal(CalendarEventStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task MovingTheEvent_ReschedulesTheActivity()
    {
        // Arrange
        var activities = new ActivityRepository();
        await using var harness = await CreateAsync(activities);
        var activity = activities.Add(NewActivity());
        var placed = await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().PlaceAsync(activity, "agent-1", "agent", TestContext.Current.CancellationToken));

        // Act: the agent drags the event on the calendar to 11:30 New York time.
        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var calendarEvent = await events.FindByIdAsync(placed.ItemId, TestContext.Current.CancellationToken);

            await events.UpdateAsync(calendarEvent, target =>
            {
                target.Start = new DateTime(2026, 11, 3, 11, 30, 0);
                target.End = new DateTime(2026, 11, 3, 11, 45, 0);
            }, new CalendarChangeOptions { Actor = "agent-1" }, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal(new DateTime(2026, 11, 3, 16, 30, 0, DateTimeKind.Utc), activity.ScheduledUtc);
        Assert.Equal(1, activities.Updates);
    }

    [Fact]
    public async Task CancellingTheEvent_LetsGoOfTheActivity_WithoutChangingItsTime()
    {
        // Arrange
        var activities = new ActivityRepository();
        await using var harness = await CreateAsync(activities);
        var activity = activities.Add(NewActivity());
        var placed = await harness.RunAsync(services => services.GetRequiredService<ActivityCalendarService>().PlaceAsync(activity, "agent-1", "agent", TestContext.Current.CancellationToken));

        // Act
        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();

            await events.CancelAsync(await events.FindByIdAsync(placed.ItemId, TestContext.Current.CancellationToken), new CalendarChangeOptions { Actor = "agent-1" }, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal(_tomorrowAtTen, activity.ScheduledUtc);
        Assert.True(activity.TryGet<ActivityCalendarLink>(out var link));
        Assert.Null(link.EventId);
    }

    private static OmnichannelActivity NewActivity()
    {
        return new OmnichannelActivity
        {
            ItemId = "activity-1",
            Status = ActivityStatus.NotStated,
            ScheduledUtc = _tomorrowAtTen,
            Channel = OmnichannelConstants.Channels.Phone,
            ContactContentItemId = "contact-1",
            PreferredDestination = "+17025550100",
            AssignedToId = "agent-1",
        };
    }

    private static Task<CalendarTestHarness> CreateAsync(ActivityRepository activities)
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(manager => manager.GetAsync("contact-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "contact-1", DisplayText = "Ada Lovelace" });

        return CalendarTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(contentManager.Object);
            services.AddScoped<ActivityCalendarService>();
            services.AddScoped<ICalendarEventHandler, ActivityCalendarEventHandler>();
            services.AddSingleton(activities.Manager);
            services.AddScoped(serviceProvider => new Lazy<IOmnichannelActivityManager>(serviceProvider.GetRequiredService<IOmnichannelActivityManager>));
        });
    }

    private static Task<CalendarEvent> FindEventAsync(CalendarTestHarness harness, string eventId)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICalendarEventManager>().FindByIdAsync(eventId, TestContext.Current.CancellationToken).AsTask(), commit: false);
    }

    // The activities, held in memory, with a manager that finds and counts updates to them.
    private sealed class ActivityRepository
    {
        private readonly Dictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);

        public ActivityRepository()
        {
            var manager = new Mock<IOmnichannelActivityManager>();

            manager
                .Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string id, CancellationToken _) => ValueTask.FromResult(_activities.GetValueOrDefault(id ?? string.Empty)));
            manager
                .Setup(m => m.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Updates++;

                    return ValueTask.CompletedTask;
                });

            Manager = manager.Object;
        }

        public IOmnichannelActivityManager Manager { get; }

        public int Updates { get; private set; }

        public OmnichannelActivity Add(OmnichannelActivity activity)
        {
            _activities[activity.ItemId] = activity;

            return activity;
        }
    }
}
