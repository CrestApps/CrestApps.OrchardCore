using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Notifications;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Every Learning notice goes to a site user through the Notifications module with a registered notification type, so
/// the user picks its delivery methods and can switch it off, and links to its record with a site-relative URL.
/// </summary>
public sealed class LearningNotificationsTests
{
    public static TheoryData<Type, string[]> NotificationTypesByStartup => new()
    {
        {
            typeof(global::CrestApps.OrchardCore.Learning.NotificationsStartup),
            [
                LearningConstants.NotificationTypes.Enrolled,
                LearningConstants.NotificationTypes.EnrollmentDeclined,
                LearningConstants.NotificationTypes.DueSoon,
                LearningConstants.NotificationTypes.Overdue,
                LearningConstants.NotificationTypes.ReviewNeeded,
                LearningConstants.NotificationTypes.EnrollmentRequested,
            ]
        },
        { typeof(AssessmentsNotificationsStartup), [AssessmentsConstants.NotificationTypes.Graded] },
        {
            typeof(global::CrestApps.OrchardCore.Learning.Certifications.NotificationsStartup),
            [
                CertificationsConstants.NotificationTypes.Issued,
                CertificationsConstants.NotificationTypes.Expiring,
                CertificationsConstants.NotificationTypes.RenewalOpened,
                CertificationsConstants.NotificationTypes.Lapsed,
                CertificationsConstants.NotificationTypes.Revoked,
            ]
        },
        {
            typeof(global::CrestApps.OrchardCore.Learning.Classes.NotificationsStartup),
            [
                ClassesConstants.NotificationTypes.Registered,
                ClassesConstants.NotificationTypes.PlaceOffered,
                ClassesConstants.SessionCancelledNotificationType,
                ClassesConstants.NotificationTypes.SessionMoved,
            ]
        },
        { typeof(global::CrestApps.OrchardCore.Learning.Commerce.NotificationsStartup), [LearningCommerceConstants.SeatGivenNotificationType] },
        {
            typeof(global::CrestApps.OrchardCore.Learning.Credentials.NotificationsStartup),
            [CredentialsConstants.NotificationType, CredentialsConstants.RevokedNotificationType]
        },
        {
            typeof(global::CrestApps.OrchardCore.Learning.Programs.NotificationsStartup),
            [
                ProgramsConstants.NotificationTypes.Required,
                ProgramsConstants.NotificationTypes.DueSoon,
                ProgramsConstants.NotificationTypes.Overdue,
                ProgramsConstants.NotificationTypes.PathCompleted,
            ]
        },
        {
            typeof(global::CrestApps.OrchardCore.Learning.Coursework.NotificationsStartup),
            [CourseworkConstants.NotificationTypes.Graded, CourseworkConstants.NotificationTypes.Returned]
        },
    };

    [Theory]
    [MemberData(nameof(NotificationTypesByStartup))]
    public void EachKindOfLearningNotice_IsARegisteredNotificationType_WithANameAndADescription(Type startupType, string[] expected)
    {
        // Arrange
        var services = new ServiceCollection();
        var startup = (IStartup)Activator.CreateInstance(startupType);

        // Act
        startup.ConfigureServices(services);
        var types = services.BuildServiceProvider().GetRequiredService<IOptions<NotificationTypeOptions>>().Value.Types;

        // Assert
        Assert.Equal(expected.Order(StringComparer.Ordinal), types.Keys.Order(StringComparer.Ordinal));

        foreach (var name in expected)
        {
            Assert.NotNull(types[name].DisplayName);
            Assert.NotNull(types[name].Description);
            Assert.True(name.Length <= 64, $"The notification type '{name}' is longer than the 64 characters Notifications stores.");
        }
    }

    [Fact]
    public async Task TheEnrolledNotice_ReachesTheLearner_WithItsType_AndASiteRelativeLinkToThePlayer()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = NotificationObserver(Courses("Fire safety"), sender);

        // Act
        await observer.EnrolledAsync(new Enrollment { ItemId = "e1", UserId = "ana", CourseId = "c1", Status = EnrollmentStatus.Active }, TestContext.Current.CancellationToken);

        // Assert
        var (userId, message) = Assert.Single(sent);
        Assert.Equal("ana", userId);
        Assert.Equal(LearningConstants.NotificationTypes.Enrolled, message.Type);
        Assert.Equal("e1", message.ReferenceId);
        Assert.Equal("~/learning/courses/c1/play", message.Url);
    }

    [Fact]
    public void ANoticeSummary_IsHtmlEncoded_BecauseNotificationsShowsItAsHtml()
    {
        var message = new LearningNotificationMessage { Subject = "Fire & <b>Safety</b>", Summary = "You can start Fire & <b>Safety</b> now." };

        Assert.Equal("You can start Fire &amp; &lt;b&gt;Safety&lt;/b&gt; now.", message.Summary);
        Assert.Equal("Fire & <b>Safety</b>", message.Subject);
    }

    [Fact]
    public async Task ADeclinedRequest_TellsTheLearner_AndOtherWithdrawalsDoNot()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = NotificationObserver(Courses("Fire safety"), sender);
        var declined = new Enrollment { ItemId = "e1", UserId = "ana", CourseId = "c1", Status = EnrollmentStatus.Withdrawn };
        var withdrawn = new Enrollment { ItemId = "e2", UserId = "ana", CourseId = "c1", Status = EnrollmentStatus.Withdrawn };

        // Act
        await observer.StatusChangedAsync(declined, EnrollmentStatus.Pending, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(withdrawn, EnrollmentStatus.Active, TestContext.Current.CancellationToken);

        // Assert
        var (userId, message) = Assert.Single(sent);
        Assert.Equal("ana", userId);
        Assert.Equal(LearningConstants.NotificationTypes.EnrollmentDeclined, message.Type);
        Assert.Equal("e1", message.ReferenceId);
        Assert.Equal("~/learning", message.Url);
    }

    [Fact]
    public async Task GradedAndReturnedCoursework_TellTheLearner_WithTheirTypes_AndLinkToTheCourseworkInThePlayer()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = new CourseworkNotificationObserver(sender, new PassThroughStringLocalizer<CourseworkNotificationObserver>());
        var submission = new Submission { ItemId = "s1", UserId = "ana", CourseId = "c1", CourseworkId = "w1", CourseworkTitle = "Risk assessment" };

        // Act
        await observer.GradedAsync(submission, null, TestContext.Current.CancellationToken);
        await observer.ReturnedAsync(submission, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            sent,
            graded =>
            {
                Assert.Equal("ana", graded.UserId);
                Assert.Equal(CourseworkConstants.NotificationTypes.Graded, graded.Message.Type);
                Assert.Equal("s1", graded.Message.ReferenceId);
                Assert.Equal("~/learning/courses/c1/play/w1", graded.Message.Url);
            },
            returned =>
            {
                Assert.Equal("ana", returned.UserId);
                Assert.Equal(CourseworkConstants.NotificationTypes.Returned, returned.Message.Type);
                Assert.Equal("~/learning/courses/c1/play/w1", returned.Message.Url);
            });
    }

    [Fact]
    public async Task ARevokedCredential_TellsTheRecipient_WithItsOwnType()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = new CredentialNotificationObserver(sender, new PassThroughStringLocalizer<CredentialNotificationObserver>());
        var award = new CredentialAward { ItemId = "a1", RecipientUserId = "ana", Achievement = new CredentialAchievement { Name = "Forklift operator" } };

        // Act
        await observer.AwardedAsync(award, TestContext.Current.CancellationToken);
        await observer.RevokedAsync(award, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CredentialsConstants.NotificationType, CredentialsConstants.RevokedNotificationType], sent.Select(item => item.Message.Type));
        Assert.All(sent, item =>
        {
            Assert.Equal("ana", item.UserId);
            Assert.Equal("a1", item.Message.ReferenceId);
            Assert.Equal("~/learning/credentials", item.Message.Url);
        });
    }

    [Fact]
    public async Task AFreedClassPlace_TellsTheLearnerWaiting_AndLinksToTheClassInThePlayer()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = ClassObserver(sender);
        var entry = new WaitlistEntry
        {
            ItemId = "w1",
            UserId = "ana",
            SessionId = "session1",
            Source = ClassesConstants.RegistrationSource,
            SourceId = ClassesConstants.RegistrationSourceId("e1", "class1"),
        };

        // Act
        await observer.OnChangedAsync(new AppointmentChange { Kind = AppointmentChangeKind.WaitlistSlotAvailable, WaitlistEntry = entry }, TestContext.Current.CancellationToken);

        // Assert
        var (userId, message) = Assert.Single(sent);
        Assert.Equal("ana", userId);
        Assert.Equal(ClassesConstants.NotificationTypes.PlaceOffered, message.Type);
        Assert.Equal("w1", message.ReferenceId);
        Assert.Equal("~/learning/courses/c1/play/class1", message.Url);
    }

    [Fact]
    public async Task AClassPlace_StaffBooked_TellsTheLearner_ButTheLearnersOwnRegistrationDoesNot()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = ClassObserver(sender);
        var appointment = new Appointment
        {
            ItemId = "ap1",
            UserId = "ana",
            SessionId = "session1",
            Source = ClassesConstants.RegistrationSource,
            SourceId = ClassesConstants.RegistrationSourceId("e1", "class1"),
        };

        // Act
        await observer.OnChangedAsync(new AppointmentChange { Kind = AppointmentChangeKind.Confirmed, Appointment = appointment, Actor = "ana" }, TestContext.Current.CancellationToken);
        await observer.OnChangedAsync(new AppointmentChange { Kind = AppointmentChangeKind.Confirmed, Appointment = appointment, Actor = "trainer" }, TestContext.Current.CancellationToken);

        // Assert
        var (userId, message) = Assert.Single(sent);
        Assert.Equal("ana", userId);
        Assert.Equal(ClassesConstants.NotificationTypes.Registered, message.Type);
        Assert.Equal("ap1", message.ReferenceId);
        Assert.Equal("~/learning/courses/c1/play/class1", message.Url);
    }

    [Fact]
    public async Task AnEnrollmentRequest_TellsTheInstructorsAndAssistantsOfTheCourse_WithALinkToTheEnrollment()
    {
        // Arrange: a course with an instructor, an assistant and a grader, who cannot approve.
        var (sender, sent) = Sender();
        var course = new ContentItem { ContentItemId = "c1", DisplayText = "Fire safety" };
        course.Alter<CoursePart>(nameof(CoursePart), part => part.Staff =
        [
            new CourseStaffMember { UserId = "tom", Role = LearningConstants.CourseStaffRoles.Instructor },
            new CourseStaffMember { UserId = "amy", Role = LearningConstants.CourseStaffRoles.Assistant },
            new CourseStaffMember { UserId = "gus", Role = LearningConstants.CourseStaffRoles.Grader },
        ]);
        var courses = new Mock<ICourseStructureService>();
        courses.Setup(service => service.GetCourseAsync("c1")).ReturnsAsync(course);
        var observer = NotificationObserver(courses.Object, sender);

        // Act
        await observer.RequestedAsync(new Enrollment { ItemId = "e1", UserId = "ana", CourseId = "c1", Status = EnrollmentStatus.Pending }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["amy", "tom"], sent.Select(item => item.UserId).Order(StringComparer.Ordinal));
        Assert.All(sent, item =>
        {
            Assert.Equal(LearningConstants.NotificationTypes.EnrollmentRequested, item.Message.Type);
            Assert.Equal("e1", item.Message.ReferenceId);
            Assert.Equal("~/Admin/learning/enrollments/e1", item.Message.Url);
            Assert.Contains("Fire safety", item.Message.Subject);
        });
    }

    [Fact]
    public async Task AnEnrollmentRequest_InACourseWithoutStaff_TellsNobody_WhenNoRoleMayApprove()
    {
        // Arrange: no course staff, and no role store to find the people who may manage enrollments.
        var (sender, sent) = Sender();
        var observer = NotificationObserver(Courses("Fire safety"), sender);

        // Act
        await observer.RequestedAsync(new Enrollment { ItemId = "e1", UserId = "ana", CourseId = "c1", Status = EnrollmentStatus.Pending }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sent);
    }

    [Fact]
    public async Task AMovedClassSession_TellsEachLearnerWithAPlace_AndLinksToTheClass_ButNotOtherAppointments()
    {
        // Arrange
        var (sender, sent) = Sender();
        var observer = ClassObserver(sender);
        var place = new Appointment
        {
            ItemId = "ap1",
            UserId = "ana",
            SessionId = "session1",
            Source = ClassesConstants.RegistrationSource,
            SourceId = ClassesConstants.RegistrationSourceId("e1", "class1"),
        };
        var booking = new Appointment { ItemId = "ap2", UserId = "bob", SessionId = "session1", Source = "Bookings" };

        // Act
        await observer.OnChangedAsync(new AppointmentChange { Kind = AppointmentChangeKind.Rescheduled, Appointment = place, Actor = "trainer" }, TestContext.Current.CancellationToken);
        await observer.OnChangedAsync(new AppointmentChange { Kind = AppointmentChangeKind.Rescheduled, Appointment = booking, Actor = "trainer" }, TestContext.Current.CancellationToken);

        // Assert
        var (userId, message) = Assert.Single(sent);
        Assert.Equal("ana", userId);
        Assert.Equal(ClassesConstants.NotificationTypes.SessionMoved, message.Type);
        Assert.Equal("ap1", message.ReferenceId);
        Assert.Equal("~/learning/courses/c1/play/class1", message.Url);
        Assert.Contains("Evening class", message.Subject);
    }

    private static LearningNotificationObserver NotificationObserver(ICourseStructureService courses, ILearningNotificationSender sender)
        => new(courses, sender, new EnrollmentApprovers(new ServiceCollection().BuildServiceProvider()), new PassThroughStringLocalizer<LearningNotificationObserver>());

    private static ClassNotificationObserver ClassObserver(ILearningNotificationSender sender)
    {
        var sessions = new Mock<ISchedulingSessionService>();
        sessions.Setup(service => service.FindByIdAsync("session1", It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSession { ItemId = "session1", Title = "Evening class" });
        var enrollments = new Mock<IEnrollmentStore>();
        enrollments.Setup(store => store.FindByIdAsync("e1", It.IsAny<CancellationToken>())).ReturnsAsync(new Enrollment { ItemId = "e1", UserId = "ana", CourseId = "c1" });

        return new ClassNotificationObserver(sessions.Object, enrollments.Object, sender, new PassThroughStringLocalizer<ClassNotificationObserver>());
    }

    private static ICourseStructureService Courses(string title)
    {
        var courses = new Mock<ICourseStructureService>();
        courses.Setup(service => service.GetCourseAsync(It.IsAny<string>())).ReturnsAsync((string id) => new ContentItem { ContentItemId = id, DisplayText = title });

        return courses.Object;
    }

    private static (ILearningNotificationSender Sender, List<(string UserId, LearningNotificationMessage Message)> Sent) Sender()
    {
        var sent = new List<(string UserId, LearningNotificationMessage Message)>();
        var sender = new Mock<ILearningNotificationSender>();
        sender.Setup(value => value.Send(It.IsAny<string>(), It.IsAny<LearningNotificationMessage>()))
            .Callback((string userId, LearningNotificationMessage message) => sent.Add((userId, message)));

        return (sender.Object, sent);
    }
}
