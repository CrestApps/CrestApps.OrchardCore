using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using CrestApps.OrchardCore.Tests.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Classes;

/// <summary>
/// Learning plan Phase 6: a capped class fills and the next learner waits; a cancellation offers the place to them
/// automatically; attendance completes the class item, and partial attendance only from its minimum; a learner can
/// only register for a class of their course while enrolled; and an enrollment that ends gives its place to the next.
/// </summary>
public sealed class ClassRegistrationTests
{
    [Fact]
    public async Task ACappedClassFills_TheNextLearnerWaits_AndACancellationPromotesThemAutomatically()
    {
        // Arrange
        var (h, session) = await CreateAsync(capacity: 1);
        await using var _ = h;
        var ana = await EnrollAsync(h, "ana");
        var ben = await EnrollAsync(h, "ben");

        // Act
        var anaPlace = await RegisterAsync(h, ana, session);
        var benPlace = await RegisterAsync(h, ben, session);
        var cancelled = await h.RunAsync(services => Registrations(services).CancelAsync(ana.ItemId, "class1", "Sick"));
        var offered = await h.RunAsync(services => Registrations(services).FindAsync(ben.ItemId, "class1"));
        var accepted = await h.RunAsync(services => Registrations(services).AcceptOfferAsync(ben.ItemId, "class1"));
        var anaAfter = await h.RunAsync(services => Registrations(services).FindAsync(ana.ItemId, "class1"));

        // Assert
        Assert.True(anaPlace.Succeeded, anaPlace.Error);
        Assert.Equal(AppointmentStatus.Confirmed, anaPlace.Registration.Appointment.Status);
        Assert.Equal(ClassesConstants.RegistrationSourceId(ana.ItemId, "class1"), anaPlace.Registration.Appointment.SourceId);
        Assert.True(benPlace.Registration.IsWaiting);
        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.True(offered.IsOffered);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(AppointmentStatus.Confirmed, accepted.Registration.Appointment.Status);
        Assert.Null(anaAfter);
    }

    [Fact]
    public async Task Attendance_CompletesTheClassItem()
    {
        // Arrange
        var (h, session) = await CreateAsync();
        await using var _ = h;
        var ana = await EnrollAsync(h, "ana");
        var place = await RegisterAsync(h, ana, session);

        // Act
        await MarkAsync(h, session, place.Registration.Appointment.ItemId, AppointmentAttendance.Attended);
        var progress = await h.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(ana.ItemId));

        // Assert
        Assert.Equal(CourseItemState.Completed, progress.Items.Single(item => item.ContentItemId == "class1").State);
    }

    [Fact]
    public async Task PartialAttendance_CompletesTheClassItem_OnlyFromItsMinimum()
    {
        // Arrange: two hours, of which at least 90 minutes count.
        var (h, session) = await CreateAsync(minimumMinutes: 90);
        await using var _ = h;
        var ana = await EnrollAsync(h, "ana");
        var ben = await EnrollAsync(h, "ben");
        var anaPlace = await RegisterAsync(h, ana, session);
        var benPlace = await RegisterAsync(h, ben, session);

        // Act
        await MarkAsync(h, session, anaPlace.Registration.Appointment.ItemId, AppointmentAttendance.Partial, 60);
        await MarkAsync(h, session, benPlace.Registration.Appointment.ItemId, AppointmentAttendance.Partial, 100);
        var anaProgress = await h.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(ana.ItemId));
        var benProgress = await h.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(ben.ItemId));

        // Assert
        Assert.NotEqual(CourseItemState.Completed, anaProgress?.Items.SingleOrDefault(item => item.ContentItemId == "class1")?.State ?? CourseItemState.NotStarted);
        Assert.Equal(CourseItemState.Completed, benProgress.Items.Single(item => item.ContentItemId == "class1").State);
    }

    [Fact]
    public async Task ARegistration_IsRefused_ForASessionOfAnotherClass_OrAnEnrollmentThatEnded()
    {
        // Arrange
        var (h, session) = await CreateAsync();
        await using var _ = h;
        var other = await SaveSessionAsync(h, session.AppointmentTypeId, session.ResourceIds, "class2", capacity: 5);
        var ana = await EnrollAsync(h, "ana");
        var ben = await EnrollAsync(h, "ben");

        await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(ben.ItemId, EnrollmentStatus.Withdrawn, "admin", "Left"));

        // Act
        var wrongSession = await h.RunAsync(services => Registrations(services).RegisterAsync(ana.ItemId, "class1", other.ItemId));
        var notAClass = await h.RunAsync(services => Registrations(services).RegisterAsync(ana.ItemId, "lesson", session.ItemId));
        var ended = await RegisterAsync(h, ben, session);

        // Assert
        Assert.Equal(ClassRegistrationErrors.SessionNotFound, wrongSession.Error);
        Assert.Equal(ClassRegistrationErrors.NotAClassOfTheCourse, notAClass.Error);
        Assert.Equal(ClassRegistrationErrors.EnrollmentNotActive, ended.Error);
    }

    [Fact]
    public async Task AnEnrollmentThatEnds_GivesItsPlaceToTheNextLearner()
    {
        // Arrange
        var (h, session) = await CreateAsync(capacity: 1);
        await using var _ = h;
        var ana = await EnrollAsync(h, "ana");
        var ben = await EnrollAsync(h, "ben");
        await RegisterAsync(h, ana, session);
        await RegisterAsync(h, ben, session);
        var withdrawn = await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(ana.ItemId, EnrollmentStatus.Withdrawn, "admin", "Left"));

        // Act: the enrollment observers run after the commit, so the test tells them as the commit would.
        await h.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<IEnrollmentObserver>())
            {
                await observer.StatusChangedAsync(withdrawn.Enrollment, EnrollmentStatus.Active);
            }

            return true;
        });
        var anaAfter = await h.RunAsync(services => Registrations(services).FindAsync(ana.ItemId, "class1"));
        var benAfter = await h.RunAsync(services => Registrations(services).FindAsync(ben.ItemId, "class1"));

        // Assert
        Assert.Null(anaAfter);
        Assert.True(benAfter.IsOffered);
    }

    [Fact]
    public async Task TwoRegistrationsAtOnce_ForTwoSessionsOfOneClass_HoldOnePlaceOnly()
    {
        // Arrange: both requests pass the one-place check before either registers, unless a lock keeps them apart.
        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var users = Users(async () =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
            {
                bothArrived.TrySetResult();
            }

            await Task.WhenAny(bothArrived.Task, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        });
        var (h, session) = await CreateAsync(users: users);
        await using var _ = h;
        var other = await SaveSessionAsync(h, session.AppointmentTypeId, session.ResourceIds, "class1", capacity: 5);
        var ana = await EnrollAsync(h, "ana");

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => h.RunAsync(services => Registrations(services).RegisterAsync(ana.ItemId, "class1", session.ItemId))),
            Task.Run(() => h.RunAsync(services => Registrations(services).RegisterAsync(ana.ItemId, "class1", other.ItemId))));
        var places = new List<Appointment>();

        foreach (var sessionId in new[] { session.ItemId, other.ItemId })
        {
            places.AddRange((await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(sessionId)))
                .Where(appointment => appointment.Status != AppointmentStatus.Cancelled));
        }

        // Assert
        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.Error == ClassRegistrationErrors.AlreadyRegistered);
        Assert.Single(places);
    }

    [Fact]
    public async Task AnAbsentLearner_OrOneWhoseAttendanceDidNotCount_CanRegisterInAnotherSession_AndGiveItUp()
    {
        // Arrange: two hours, of which at least 90 minutes count.
        var (h, session) = await CreateAsync(minimumMinutes: 90);
        await using var _ = h;
        var other = await SaveSessionAsync(h, session.AppointmentTypeId, session.ResourceIds, "class1", capacity: 5);
        var ana = await EnrollAsync(h, "ana");
        var ben = await EnrollAsync(h, "ben");
        var cid = await EnrollAsync(h, "cid");
        var anaPlace = await RegisterAsync(h, ana, session);
        var benPlace = await RegisterAsync(h, ben, session);
        var cidPlace = await RegisterAsync(h, cid, session);

        await MarkAsync(h, session, anaPlace.Registration.Appointment.ItemId, AppointmentAttendance.Absent);
        await MarkAsync(h, session, benPlace.Registration.Appointment.ItemId, AppointmentAttendance.Partial, 60);
        await MarkAsync(h, session, cidPlace.Registration.Appointment.ItemId, AppointmentAttendance.Partial, 100);

        // Act
        var anaAgain = await RegisterAsync(h, ana, other);
        var benAgain = await RegisterAsync(h, ben, other);
        var cidAgain = await RegisterAsync(h, cid, other);
        var anaFound = await h.RunAsync(services => Registrations(services).FindAsync(ana.ItemId, "class1"));
        var anaGaveUp = await h.RunAsync(services => Registrations(services).CancelAsync(ana.ItemId, "class1", "Busy"));

        // Assert
        Assert.True(anaAgain.Succeeded, anaAgain.Error);
        Assert.Equal(other.ItemId, anaAgain.Registration.Session.ItemId);
        Assert.Equal(AppointmentStatus.Confirmed, anaAgain.Registration.Appointment.Status);
        Assert.True(benAgain.Succeeded, benAgain.Error);
        Assert.Equal(other.ItemId, benAgain.Registration.Session.ItemId);
        Assert.Equal(ClassRegistrationErrors.AlreadyRegistered, cidAgain.Error);
        Assert.Equal(other.ItemId, anaFound.Session.ItemId);
        Assert.True(anaGaveUp.Succeeded, anaGaveUp.Error);
    }

    [Fact]
    public async Task MarkingAnAbsentLearnerNotMarked_RestoresTheirConfirmedPlace()
    {
        // Arrange
        var (h, session) = await CreateAsync();
        await using var _ = h;
        var ana = await EnrollAsync(h, "ana");
        var place = await RegisterAsync(h, ana, session);
        var appointmentId = place.Registration.Appointment.ItemId;

        await MarkAsync(h, session, appointmentId, AppointmentAttendance.Absent);

        // Act
        await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().RecordAttendanceAsync(session.ItemId, [new SessionAttendanceMark(appointmentId, AppointmentAttendance.Unmarked)]));
        var after = (await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(session.ItemId))).Single(item => item.ItemId == appointmentId);

        // Assert
        Assert.Equal(AppointmentStatus.Confirmed, after.Status);
        Assert.Equal(AppointmentAttendance.Unmarked, after.Attendance);
        Assert.Null(after.MinutesAttended);
    }

    private static IClassRegistrationService Registrations(IServiceProvider services)
        => services.GetRequiredService<IClassRegistrationService>();

    private static Task<ClassRegistrationResult> RegisterAsync(CalendarTestHarness h, Enrollment enrollment, SchedulingSession session)
        => h.RunAsync(services => Registrations(services).RegisterAsync(enrollment.ItemId, "class1", session.ItemId));

    // Marks attendance, then tells the appointment observers what the session service queued for after the commit.
    private static async Task MarkAsync(CalendarTestHarness h, SchedulingSession session, string appointmentId, AppointmentAttendance attendance, int? minutes = null)
    {
        await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().RecordAttendanceAsync(session.ItemId, [new SessionAttendanceMark(appointmentId, attendance, minutes)]));

        var appointment = (await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(session.ItemId))).Single(item => item.ItemId == appointmentId);

        await h.RunAsync(async services =>
        {
            foreach (var observer in services.GetServices<IAppointmentObserver>())
            {
                await observer.OnChangedAsync(new AppointmentChange { Kind = attendance == AppointmentAttendance.Absent ? AppointmentChangeKind.NoShow : AppointmentChangeKind.Completed, Appointment = appointment });
            }

            return true;
        });
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness h, string userId)
    {
        var result = await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest { UserId = userId, CourseId = "c1", Source = "Test", SourceId = userId, Actor = "admin" }));

        Assert.NotNull(result.Enrollment);

        return result.Enrollment;
    }

    private static async Task<(CalendarTestHarness Harness, SchedulingSession Session)> CreateAsync(int capacity = 5, int? minimumMinutes = null, UserManager<IUser> users = null)
    {
        var clock = new TestClock { UtcNow = CalendarTestHarness.Now };
        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                services.AddSchedulingCore();
                services.AddLearningClassesCore();
                services.RemoveAll<IDistributedLock>();
                services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddSingleton(users ?? Users());
            },
            async schemaBuilder =>
            {
                await SchedulingIndexSchema.CreateAsync(schemaBuilder);
                await SchedulingSessionIndexSchema.CreateAsync(schemaBuilder);
            },
            [new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider(), .. SchedulingSessionIndexSchema.Providers()],
            [SchedulingConstants.CollectionName]);

        courses.AddCourse("c1");
        courses.AddItem("c1", "lesson");
        courses.AddItem("c1", "class1", kind: ClassesConstants.ClassKind, completionMode: CompletionModes.Attended).Apply(new ClassPart { MinimumMinutes = minimumMinutes });
        courses.AddItem("c1", "class2", kind: ClassesConstants.ClassKind, completionMode: CompletionModes.Attended).Apply(new ClassPart());

        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var session = await SaveSessionAsync(harness, type.ItemId, [resource.ItemId], "class1", capacity);

        return (harness, session);
    }

    private static async Task<SchedulingSession> SaveSessionAsync(CalendarTestHarness h, string typeId, IList<string> resourceIds, string classItemId, int capacity)
    {
        var start = SchedulingTestSupport.Utc(SchedulingTestSupport.Day.AddDays(7).AddHours(9));
        var session = new SchedulingSession
        {
            AppointmentTypeId = typeId,
            Title = "Forklift practical",
            Capacity = capacity,
            ResourceIds = resourceIds.ToList(),
            WaitlistMode = SchedulingWaitlistMode.AutoPromote,
            Source = ClassesConstants.SessionSource,
            SourceId = classItemId,
            Meetings = [new SchedulingSessionMeeting { StartUtc = start, EndUtc = start.AddHours(2) }],
        };

        Assert.Empty(await h.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().SaveAsync(session)));

        return session;
    }

    // The user lookup sits between the one-place check and the registration, so a race test can hold requests there.
    private static UserManager<IUser> Users(Func<Task> onLookup = null)
    {
        var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        foreach (var name in new[] { "ana", "ben", "cid" })
        {
            var user = Mock.Of<IUser>(value => value.UserName == name);

            users.Setup(manager => manager.FindByIdAsync(name)).Returns(async () =>
            {
                if (onLookup is not null)
                {
                    await onLookup();
                }

                return user;
            });
            users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync(name + "@example.com");
            users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync(name);
        }

        return users.Object;
    }
}
