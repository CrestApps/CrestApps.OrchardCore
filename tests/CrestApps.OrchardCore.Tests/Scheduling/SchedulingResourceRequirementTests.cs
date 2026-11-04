using System.Collections.Concurrent;
using System.Data.Common;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using static CrestApps.OrchardCore.Tests.Scheduling.SchedulingTestSupport;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// Appointment types that need a resource from each of several groups, such as one person from the staff and one room: open
/// times are where every group has someone or something free, each group picks its own way, round robin follows weights, a
/// booking locks and versions every resource it could take, and types with a single resource list keep working as before.
/// </summary>
public sealed class SchedulingResourceRequirementTests
{
    [Fact]
    public async Task TwoGroups_OfferOnlyTimesWhenEachGroupHasAFreeResource()
    {
        // Arrange (both staff are busy at 9:00, the only room is busy at 13:00)
        await using var harness = await CreateAsync();
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");

        await harness.CreateEventAsync(ann.CalendarId, Day.AddHours(9), Day.AddHours(10));
        await harness.CreateEventAsync(bob.CalendarId, Day.AddHours(9), Day.AddHours(10));
        await harness.CreateEventAsync(room.CalendarId, Day.AddHours(13), Day.AddHours(14));

        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Staff", ResourceIds = [ann.ItemId, bob.ItemId] },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        // Act
        var slots = await FindAsync(harness, type, Day, Day.AddDays(1));
        var booked = await BookAsync(harness, Request(type, Day.AddHours(10)));
        var afterBooking = await FindAsync(harness, type, Day, Day.AddDays(1));
        var withBob = await FindAsync(harness, type, Day, Day.AddDays(1), bob.ItemId);

        // Assert (once the room is taken at 10:00, Bob being free doesn't make the time open)
        Assert.Equal(["Tue 10:00", "Tue 11:00", "Tue 14:00", "Tue 15:00", "Tue 16:00"], Local(slots));
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.Equal([ann.ItemId, room.ItemId], booked.Appointment.ResourceIds);
        Assert.Equal(["Tue 11:00", "Tue 14:00", "Tue 15:00", "Tue 16:00"], Local(afterBooking));
        Assert.All(withBob, slot => Assert.Equal([bob.ItemId, room.ItemId], slot.ResourceIds));
    }

    [Fact]
    public async Task EachGroup_PicksItsOwnWay()
    {
        // Arrange (a doctor and a nurse together, and any free room)
        await using var harness = await CreateAsync();
        var doctor = await AddResourceAsync(harness, "Doctor");
        var nurse = await AddResourceAsync(harness, "Nurse");
        var roomOne = await AddResourceAsync(harness, "Room 1");
        var roomTwo = await AddResourceAsync(harness, "Room 2");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Team", ResourceIds = [doctor.ItemId, nurse.ItemId], Assignment = AppointmentAssignment.AllTogether },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [roomOne.ItemId, roomTwo.ItemId] },
        ]);

        await harness.CreateEventAsync(roomOne.CalendarId, Day.AddHours(9), Day.AddHours(10));
        await harness.CreateEventAsync(nurse.CalendarId, Day.AddHours(10), Day.AddHours(11));

        // Act
        var nineOClock = await BookAsync(harness, Request(type, Day.AddHours(9)));
        var tenOClock = await BookAsync(harness, Request(type, Day.AddHours(10), "Jane Doe", "jane@contoso.com"));

        // Assert (at 9:00 the first room is busy, so the second is taken; at 10:00 the nurse is busy, so there is no team)
        Assert.True(nineOClock.Succeeded, string.Join(" ", nineOClock.Errors));
        Assert.Equal(new[] { doctor.ItemId, nurse.ItemId, roomTwo.ItemId }.Order(StringComparer.Ordinal), nineOClock.Appointment.ResourceIds.Order(StringComparer.Ordinal));
        Assert.False(tenOClock.Succeeded);
        Assert.True(tenOClock.Conflict);
    }

    [Fact]
    public async Task RoundRobin_FollowsTheWeights()
    {
        // Arrange (Ann takes three shares, Bob one)
        await using var harness = await CreateAsync();
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement
            {
                Name = "Staff",
                ResourceIds = [ann.ItemId, bob.ItemId],
                Assignment = AppointmentAssignment.RoundRobin,
                Weights = new Dictionary<string, int> { [ann.ItemId] = 3 },
            },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        var starts = new[] { 9, 10, 11, 13, 14, 15, 16 }.Select(hour => Day.AddHours(hour)).Append(Day.AddDays(1).AddHours(9)).ToList();
        var staff = new List<string>();

        // Act
        foreach (var start in starts)
        {
            var result = await BookAsync(harness, Request(type, start, "Customer " + staff.Count, $"customer{staff.Count}@contoso.com"));

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            staff.Add(result.Appointment.ResourceIds[0]);
        }

        // Assert (three to one: Ann, Bob, Ann, Ann, Ann, Bob, Ann, Ann)
        Assert.Equal(6, staff.Count(id => id == ann.ItemId));
        Assert.Equal(2, staff.Count(id => id == bob.ItemId));
        Assert.Equal([ann.ItemId, bob.ItemId, ann.ItemId, ann.ItemId, ann.ItemId, bob.ItemId, ann.ItemId, ann.ItemId], staff);
    }

    [Fact]
    public async Task ConcurrentBookings_OfTheLastRoom_OnlyOneCommits()
    {
        // Arrange (two customers ask for different staff at the same time, with the only room; the lock really serializes)
        await using var harness = await CreateAsync(services => services.AddSingleton<IDistributedLock, SerializingLock>());
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Staff", ResourceIds = [ann.ItemId, bob.ItemId] },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        var withAnn = Request(type, Day.AddHours(14), "First", "first@contoso.com");
        var withBob = Request(type, Day.AddHours(14), "Second", "second@contoso.com");

        withAnn.ResourceId = ann.ItemId;
        withBob.ResourceId = bob.ItemId;

        // Act (each request books and commits in its own scope, at the same time)
        var outcomes = await Task.WhenAll(Task.Run(() => TryBookAsync(harness, withAnn)), Task.Run(() => TryBookAsync(harness, withBob)));
        var roomAppointments = await harness.RunAsync(services => services.GetRequiredService<AppointmentStore>().ListForResourcesAsync([room.ItemId], Utc(Day), Utc(Day.AddDays(1))), commit: false);
        var roomVersion = await harness.RunAsync(async services => (await services.GetRequiredService<SchedulingResourceStore>().FindByIdAsync(room.ItemId)).BookingVersion, commit: false);
        var again = await BookAsync(harness, Request(type, Day.AddHours(14), "Third", "third@contoso.com"));

        // Assert (whichever came second either found the room taken or failed its commit on the room's version)
        Assert.Single(outcomes, committed => committed);
        var appointment = Assert.Single(roomAppointments);
        Assert.Contains(room.ItemId, appointment.ResourceIds);
        Assert.Equal(1, roomVersion);
        Assert.False(again.Succeeded);
        Assert.True(again.Conflict);
    }

    [Fact]
    public async Task Booking_BumpsTheVersionOfEveryResourceItTakes()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Staff", ResourceIds = [ann.ItemId, bob.ItemId] },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        // Act
        var booked = await BookAsync(harness, Request(type, Day.AddHours(9)));
        var versions = await harness.RunAsync(
            async services => (await services.GetRequiredService<SchedulingResourceStore>().GetAsync([ann.ItemId, bob.ItemId, room.ItemId])).ToDictionary(resource => resource.ItemId, resource => resource.BookingVersion),
            commit: false);

        // Assert (the resources it took are guarded; the one it didn't take is left alone)
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.Equal(1, versions[ann.ItemId]);
        Assert.Equal(1, versions[room.ItemId]);
        Assert.Equal(0, versions[bob.ItemId]);
    }

    [Fact]
    public async Task Booking_LocksEveryResourceOfEveryGroup_InOrder()
    {
        // Arrange
        var locks = new RecordingLock();

        await using var harness = await CreateAsync(services => services.AddSingleton<IDistributedLock>(locks));
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Staff", ResourceIds = [ann.ItemId, bob.ItemId] },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        // Act
        var booked = await BookAsync(harness, Request(type, Day.AddHours(9)));

        // Assert
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        var expected = new[] { ann.ItemId, bob.ItemId, room.ItemId }.Select(id => "SCHEDULING_RESOURCE_" + id).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, locks.Keys.Where(key => key.StartsWith("SCHEDULING_", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Reschedule_KeepsFreeResources_AndSwapsBusyOnes()
    {
        // Arrange (Ann is busy at 14:00, so a move there needs Bob; the room stays)
        await using var harness = await CreateAsync();
        var ann = await AddResourceAsync(harness, "Ann");
        var bob = await AddResourceAsync(harness, "Bob");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { Name = "Staff", ResourceIds = [ann.ItemId, bob.ItemId] },
            new AppointmentResourceRequirement { Name = "Room", ResourceIds = [room.ItemId] },
        ]);

        await harness.CreateEventAsync(ann.CalendarId, Day.AddHours(14), Day.AddHours(15));

        var booked = await BookAsync(harness, Request(type, Day.AddHours(9)));

        // Act
        var keep = await MoveAsync(harness, booked.Appointment.ItemId, Day.AddHours(10));
        var swap = await MoveAsync(harness, booked.Appointment.ItemId, Day.AddHours(14));

        // Assert
        Assert.True(keep.Succeeded, string.Join(" ", keep.Errors));
        Assert.Equal([ann.ItemId, room.ItemId], keep.Appointment.ResourceIds);
        Assert.True(swap.Succeeded, string.Join(" ", swap.Errors));
        Assert.Equal([bob.ItemId, room.ItemId], swap.Appointment.ResourceIds);
    }

    [Fact]
    public async Task TypeWithOnlyAResourceList_ReadsAsOneRequirement_AndBooksAsBefore()
    {
        // Arrange (a type saved before resource groups existed: a list and "all together")
        await using var harness = await CreateAsync();
        var doctor = await AddResourceAsync(harness, "Doctor");
        var room = await AddResourceAsync(harness, "Room 1");
        var type = await AddTypeAsync(harness, [doctor.ItemId, room.ItemId], t =>
        {
            t.Assignment = AppointmentAssignment.AllTogether;
            t.ResourceRequirements = null;
        });

        await harness.CreateEventAsync(room.CalendarId, Day.AddHours(9), Day.AddHours(10));

        // Act
        var requirement = Assert.Single(type.GetResourceRequirements());
        var slots = await FindAsync(harness, type, Day, Day.AddDays(1));
        var booked = await BookAsync(harness, Request(type, Day.AddHours(10)));

        // Assert
        Assert.Equal([doctor.ItemId, room.ItemId], requirement.ResourceIds);
        Assert.Equal(AppointmentAssignment.AllTogether, requirement.Assignment);
        Assert.DoesNotContain("Tue 9:00", Local(slots));
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.Equal(2, booked.Appointment.ResourceIds.Count);
    }

    [Fact]
    public async Task AllTogether_TheSecondResourceIsBusyForOtherTypes()
    {
        // Arrange (the room's calendar doesn't hold the appointment, which sits on the doctor's calendar)
        await using var harness = await CreateAsync();
        var doctor = await AddResourceAsync(harness, "Doctor");
        var nurse = await AddResourceAsync(harness, "Nurse");
        var room = await AddResourceAsync(harness, "Room 1");
        var exam = await AddTypeAsync(harness, [doctor.ItemId, room.ItemId], t => t.Assignment = AppointmentAssignment.AllTogether);
        var shots = await AddTypeAsync(harness, [], t => t.ResourceRequirements =
        [
            new AppointmentResourceRequirement { ResourceIds = [nurse.ItemId] },
            new AppointmentResourceRequirement { ResourceIds = [room.ItemId] },
        ]);

        // Act
        var booked = await BookAsync(harness, Request(exam, Day.AddHours(9)));
        var shotSlots = await FindAsync(harness, shots, Day, Day.AddDays(1));

        // Assert
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.DoesNotContain("Tue 9:00", Local(shotSlots));
        Assert.Contains("Tue 10:00", Local(shotSlots));
    }

    private static Task<AppointmentResult> MoveAsync(CalendarTestHarness harness, string appointmentId, DateTime startLocal)
    {
        return harness.RunAsync(async services =>
        {
            var appointments = services.GetRequiredService<IAppointmentService>();

            return await appointments.RescheduleAsync(await appointments.FindByIdAsync(appointmentId), Utc(startLocal));
        });
    }

    private static async Task<bool> TryBookAsync(CalendarTestHarness harness, AppointmentRequest request)
    {
        await using var scope = harness.CreateScope();

        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<IAppointmentService>().BookAsync(request);

            if (!result.Succeeded)
            {
                return false;
            }

            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync();

            return true;
        }
        catch (Exception exception) when (exception is ConcurrencyException or DbException)
        {
            // The other booking won: its version check failed this commit, or SQLite (one writer at a time) refused it.
            return false;
        }
    }

    // An in-process lock that really holds each key, like the distributed lock does across servers.
    private sealed class SerializingLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keys = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _keys.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();

            return new Locker(semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            var semaphore = _keys.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            return await semaphore.WaitAsync(timeout) ? ((ILocker)new Locker(semaphore), true) : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
        {
            return Task.FromResult(_keys.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);
        }

        private sealed class Locker : ILocker
        {
            private readonly SemaphoreSlim _semaphore;

            public Locker(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public ValueTask DisposeAsync()
            {
                _semaphore.Release();

                return ValueTask.CompletedTask;
            }

            public void Dispose()
            {
                _semaphore.Release();
            }
        }
    }

    private sealed class RecordingLock : IDistributedLock
    {
        public List<string> Keys { get; } = [];

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            Keys.Add(key);

            return Task.FromResult<ILocker>(new Locker());
        }

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            Keys.Add(key);

            return Task.FromResult<(ILocker, bool)>((new Locker(), true));
        }

        public Task<bool> IsLockAcquiredAsync(string key)
        {
            return Task.FromResult(false);
        }

        private sealed class Locker : ILocker
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
