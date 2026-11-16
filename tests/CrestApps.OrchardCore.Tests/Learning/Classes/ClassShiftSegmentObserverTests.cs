using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning.Classes;

/// <summary>
/// Learning plan Phase 6, a staff class shows on the shift plan: a confirmed place puts each meeting of the session on
/// the staff member's shifts as Training, and a cancelled place takes it back; learners who aren't staff and
/// appointments that aren't class registrations are left alone.
/// </summary>
public sealed class ClassShiftSegmentObserverTests
{
    private static readonly DateTime _firstStart = new(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _secondStart = new(2026, 11, 18, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AConfirmedPlace_OfAStaffLearner_PlacesOneTrainingSegmentPerMeeting()
    {
        // Arrange
        var kit = new Kit();
        var placements = new List<ShiftSegmentPlacement>();

        kit.Shifts
            .Setup(service => service.PlaceSegmentAsync(It.IsAny<ShiftSegmentPlacement>(), It.IsAny<CancellationToken>()))
            .Callback<ShiftSegmentPlacement, CancellationToken>((placement, _) => placements.Add(placement))
            .ReturnsAsync(new PlanningResult<IReadOnlyList<CalendarEvent>>());

        // Act
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Confirmed, Registration("user-ana")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, placements.Count);
        Assert.All(placements, placement =>
        {
            Assert.Equal(("staff-ana", ActivityKind.Training, ClassesConstants.RegistrationSource, "appointment-1"), (placement.WorkerId, placement.Kind, placement.Source, placement.SourceId));
            Assert.Equal("Class: First aid", placement.Reason);
        });
        Assert.Equal([(_firstStart, _firstStart.AddHours(2)), (_secondStart, _secondStart.AddHours(2))], placements.Select(placement => (placement.StartUtc, placement.EndUtc)));
    }

    [Fact]
    public async Task ANoShiftThen_Failure_IsIgnored()
    {
        // Arrange
        var kit = new Kit();
        var failed = new PlanningResult<IReadOnlyList<CalendarEvent>>();

        failed.Fail("No published shift covers that time.");
        kit.Shifts
            .Setup(service => service.PlaceSegmentAsync(It.IsAny<ShiftSegmentPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failed);

        // Act
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Confirmed, Registration("user-ana")), TestContext.Current.CancellationToken);

        // Assert
        kit.Shifts.Verify(service => service.PlaceSegmentAsync(It.IsAny<ShiftSegmentPlacement>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ALearnerWhoIsNotStaff_OrAnAppointmentThatIsNotAClass_PlacesNothing()
    {
        // Arrange
        var kit = new Kit();
        var other = Registration("user-ana");

        other.Source = "Bookings";

        // Act
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Confirmed, Registration("user-ben")), TestContext.Current.CancellationToken);
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Confirmed, other), TestContext.Current.CancellationToken);
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Held, Registration("user-ana")), TestContext.Current.CancellationToken);

        // Assert
        kit.Shifts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ACancelledPlace_RemovesTheSegmentsOverTheSessionsSpan()
    {
        // Arrange
        var kit = new Kit();

        kit.Shifts
            .Setup(service => service.RemoveSegmentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        await kit.Observer.OnChangedAsync(Change(AppointmentChangeKind.Cancelled, Registration("user-ana")), TestContext.Current.CancellationToken);

        // Assert
        kit.Shifts.Verify(service => service.RemoveSegmentsAsync("staff-ana", ClassesConstants.RegistrationSource, "appointment-1", _firstStart, _secondStart.AddHours(2), "tester", It.IsAny<CancellationToken>()), Times.Once);
        kit.Shifts.VerifyNoOtherCalls();
    }

    private static Appointment Registration(string userId)
    {
        return new Appointment
        {
            ItemId = "appointment-1",
            UserId = userId,
            SessionId = "session-1",
            Source = ClassesConstants.RegistrationSource,
            SourceId = ClassesConstants.RegistrationSourceId("enrollment-1", "class-1"),
            Status = AppointmentStatus.Confirmed,
            StartUtc = _firstStart,
            EndUtc = _firstStart.AddHours(2),
        };
    }

    private static AppointmentChange Change(AppointmentChangeKind kind, Appointment appointment)
        => new() { Kind = kind, Appointment = appointment, Actor = "tester" };

    private sealed class Kit
    {
        public Kit()
        {
            var staff = new Mock<IStaffDirectory>();
            var sessions = new Mock<ISchedulingSessionService>();

            staff.Setup(directory => directory.FindByUserIdAsync("user-ana", It.IsAny<CancellationToken>())).ReturnsAsync(new StaffMember { ItemId = "staff-ana", UserId = "user-ana" });
            sessions.Setup(service => service.FindByIdAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSession
            {
                ItemId = "session-1",
                Title = "First aid",
                Meetings =
                [
                    new SchedulingSessionMeeting { StartUtc = _firstStart, EndUtc = _firstStart.AddHours(2) },
                    new SchedulingSessionMeeting { StartUtc = _secondStart, EndUtc = _secondStart.AddHours(2) },
                ],
            });

            var services = new ServiceCollection()
                .AddSingleton(Shifts.Object)
                .AddSingleton(staff.Object)
                .AddSingleton(sessions.Object)
                .BuildServiceProvider();

            Observer = new ClassShiftSegmentObserver(services, NullLogger<ClassShiftSegmentObserver>.Instance);
        }

        public Mock<IShiftService> Shifts { get; } = new();

        public ClassShiftSegmentObserver Observer { get; }
    }
}
