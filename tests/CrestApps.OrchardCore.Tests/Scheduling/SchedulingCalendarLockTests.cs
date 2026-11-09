using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// A scheduling resource linked to a calendar that limits overlapping events, such as a room, books under the calendar's
/// own lock and runs the calendar's overlap check, so an appointment and an event added to the room directly can never
/// take the same time.
/// </summary>
public sealed class SchedulingCalendarLockTests
{
    [Fact]
    public async Task BookAsync_WhileTheLinkedRoomIsBeingBookedInCalendars_IsRefusedAsBusy()
    {
        // Arrange: a calendar request holds the room's lock.
        var locks = new HoldingLock();
        await using var harness = await SchedulingTestSupport.CreateAsync(services => services.Replace(ServiceDescriptor.Singleton<IDistributedLock>(locks)));
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Room 1");
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Room 1", resource => resource.CalendarId = room.ItemId);
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);

        locks.Held.Add(CalendarLockKeys.Calendar(room.ItemId));

        // Act
        var result = await SchedulingTestSupport.BookAsync(harness, SchedulingTestSupport.Request(type, SchedulingTestSupport.Day.AddHours(10)));

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Conflict);
        Assert.Contains(CalendarLockKeys.Calendar(room.ItemId), locks.Requested);
    }

    [Fact]
    public async Task AnEventInTheRoom_WhileAnAppointmentIsBeingBooked_IsRefusedAsBusy()
    {
        // Arrange: an appointment booking holds the room's lock, under the same name.
        var locks = new HoldingLock();
        await using var harness = await SchedulingTestSupport.CreateAsync(services => services.Replace(ServiceDescriptor.Singleton<IDistributedLock>(locks)));
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Room 1");

        locks.Held.Add(CalendarLockKeys.Calendar(room.ItemId));

        // Act
        var created = await harness.CreateEventAsync(room.ItemId, SchedulingTestSupport.Day.AddHours(10), SchedulingTestSupport.Day.AddHours(11));

        // Assert
        Assert.False(created.Succeeded);
        Assert.Contains(created.Errors, error => error.Code == CalendarError.ConflictCode);
    }

    [Fact]
    public async Task BookAsync_WhenTheRoomWasTakenInCalendarsAfterTheTimeLookedFree_IsRefusedByTheRoomsOverlapCheck()
    {
        // Arrange: availability was read before the room's own event was saved, as in a race, so it still offers 10:00.
        await using var harness = await SchedulingTestSupport.CreateAsync(services => services.Replace(ServiceDescriptor.Scoped<ISchedulingAvailabilityService, StaleAvailability>()));
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Room 1");
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Room 1", resource => resource.CalendarId = room.ItemId);
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var meeting = await harness.CreateEventAsync(room.ItemId, SchedulingTestSupport.Day.AddHours(10), SchedulingTestSupport.Day.AddHours(11));

        Assert.True(meeting.Succeeded);

        // Act
        var result = await SchedulingTestSupport.BookAsync(harness, SchedulingTestSupport.Request(type, SchedulingTestSupport.Day.AddHours(10)));
        var occurrences = await SchedulingTestSupport.OccurrencesAsync(harness, room.ItemId);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Single(occurrences, occurrence => occurrence.Event.Status != CalendarEventStatus.Cancelled);
    }

    private sealed class StaleAvailability : ISchedulingAvailabilityService
    {
        public Task<IReadOnlyList<AppointmentSlot>> FindSlotsAsync(AppointmentType appointmentType, DateTime fromUtc, DateTime toUtc, string resourceId = null, int maxResults = 200, string excludeAppointmentId = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AppointmentSlot> slots =
            [
                new AppointmentSlot
                {
                    StartUtc = fromUtc,
                    EndUtc = fromUtc.AddMinutes(appointmentType.DurationMinutes),
                    ResourceIds = [.. appointmentType.ResourceIds],
                    Remaining = 1,
                },
            ];

            return Task.FromResult(slots);
        }
    }

    private sealed class HoldingLock : IDistributedLock
    {
        public HashSet<string> Held { get; } = [];

        public List<string> Requested { get; } = [];

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new Locker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            Requested.Add(key);

            return Task.FromResult<(ILocker, bool)>(Held.Contains(key) ? (null, false) : (new Locker(), true));
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(Held.Contains(key));
    }

    private sealed class Locker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
