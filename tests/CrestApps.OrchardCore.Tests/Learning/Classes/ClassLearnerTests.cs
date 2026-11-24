using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Controllers;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning.Classes;

/// <summary>
/// Learning plan Phase 6, the class panel: the forms act on the signed-in learner's own open enrollment in the course
/// of the class item, found from the class item and never taken from the form. A learner who is not enrolled is
/// refused, and another learner's enrollment is never used.
/// </summary>
public sealed class ClassLearnerTests
{
    private const string ClassItemId = "class1";
    private const string CourseId = "course1";

    [Fact]
    public async Task Resolve_ALearnerWhoIsNotEnrolled_HasNoEnrollment()
    {
        // Arrange
        var resolver = CreateResolver([]);

        // Act
        var learner = await resolver.ResolveAsync("ana", ClassItemId);

        // Assert
        Assert.NotNull(learner);
        Assert.Equal(CourseId, learner.CourseId);
        Assert.Null(learner.Enrollment);
    }

    [Fact]
    public async Task Resolve_NeverUsesAnotherLearnersEnrollment()
    {
        // Arrange
        var resolver = CreateResolver([Enrollment("e-ben", "ben", EnrollmentStatus.Active)]);

        // Act
        var learner = await resolver.ResolveAsync("ana", ClassItemId);

        // Assert
        Assert.Null(learner.Enrollment);
    }

    [Fact]
    public async Task Resolve_UsesTheNewestOpenEnrollmentOfTheLearner()
    {
        // Arrange
        var resolver = CreateResolver(
        [
            Enrollment("e-new", "ana", EnrollmentStatus.Withdrawn),
            Enrollment("e-open", "ana", EnrollmentStatus.Active),
            Enrollment("e-old", "ana", EnrollmentStatus.Suspended),
        ]);

        // Act
        var learner = await resolver.ResolveAsync("ana", ClassItemId);

        // Assert
        Assert.Equal("e-open", learner.Enrollment.ItemId);
    }

    [Fact]
    public async Task Resolve_ALearnerWhoseEnrollmentEnded_StillSeesTheirOwnEnrollment()
    {
        // Arrange: the class was the last item, so attending it completed the course. Found in the browser.
        var resolver = CreateResolver(
        [
            Enrollment("e-done", "ana", EnrollmentStatus.Completed),
            Enrollment("e-other", "ben", EnrollmentStatus.Active),
        ]);

        // Act
        var learner = await resolver.ResolveAsync("ana", ClassItemId);

        // Assert
        Assert.Equal("e-done", learner.Enrollment?.ItemId);
    }

    [Fact]
    public async Task Resolve_AnItemThatIsNotAClass_IsNotFound()
    {
        // Arrange
        var resolver = CreateResolver([Enrollment("e-ana", "ana", EnrollmentStatus.Active)], classPart: false);

        // Act
        var learner = await resolver.ResolveAsync("ana", ClassItemId);

        // Assert
        Assert.Null(learner);
    }

    [Fact]
    public async Task Resolve_WithoutASignedInLearner_FindsNothing()
    {
        // Arrange
        var resolver = CreateResolver([Enrollment("e-ana", "ana", EnrollmentStatus.Active)]);

        // Act
        var learner = await resolver.ResolveAsync(null, ClassItemId);

        // Assert
        Assert.Null(learner);
    }

    [Fact]
    public async Task Register_ALearnerWhoIsNotEnrolled_IsRefused_AndNothingIsRegistered()
    {
        // Arrange
        var registrations = new Mock<IClassRegistrationService>();
        var notifier = new Mock<INotifier>();
        var controller = CreateController("ana", [Enrollment("e-ben", "ben", EnrollmentStatus.Active)], registrations, notifier);

        // Act
        var result = await controller.Register(ClassItemId, "s1");

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        Assert.Empty(registrations.Invocations);
        Assert.Contains(notifier.Invocations, invocation => invocation.Arguments.OfType<NotifyType>().Contains(NotifyType.Warning));
    }

    [Fact]
    public async Task Register_UsesTheSignedInLearnersOwnEnrollment()
    {
        // Arrange
        var registrations = new Mock<IClassRegistrationService>();
        registrations
            .Setup(service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClassRegistrationResult { Registration = new ClassRegistration(new SchedulingSession { ItemId = "s1" }, new Appointment { Status = AppointmentStatus.Confirmed }, null) });
        var controller = CreateController(
            "ana",
            [
                Enrollment("e-ben", "ben", EnrollmentStatus.Active),
                Enrollment("e-ana", "ana", EnrollmentStatus.Active),
            ],
            registrations,
            new Mock<INotifier>());

        // Act
        var result = await controller.Register(ClassItemId, "s1");

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        registrations.Verify(service => service.RegisterAsync("e-ana", ClassItemId, "s1", "ana", It.IsAny<CancellationToken>()), Times.Once);
        registrations.Verify(service => service.RegisterAsync("e-ben", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_WithoutASignedInLearner_IsChallenged()
    {
        // Arrange
        var registrations = new Mock<IClassRegistrationService>();
        var controller = CreateController(null, [Enrollment("e-ana", "ana", EnrollmentStatus.Active)], registrations, new Mock<INotifier>());

        // Act
        var result = await controller.Cancel(ClassItemId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
        Assert.Empty(registrations.Invocations);
    }

    [Fact]
    public async Task Accept_ARefusedChange_TellsTheLearnerWhy()
    {
        // Arrange
        var registrations = new Mock<IClassRegistrationService>();
        registrations
            .Setup(service => service.AcceptOfferAsync("e-ana", ClassItemId, "ana", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassRegistrationResult.Fail(ClassRegistrationErrors.NoOffer));
        var notifier = new Mock<INotifier>();
        var controller = CreateController("ana", [Enrollment("e-ana", "ana", EnrollmentStatus.Active)], registrations, notifier);

        // Act
        var result = await controller.Accept(ClassItemId);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        Assert.Contains(notifier.Invocations, invocation => invocation.Arguments.OfType<NotifyType>().Contains(NotifyType.Warning));
    }

    private static ClassLearnerResolver CreateResolver(IReadOnlyList<Enrollment> enrollmentsOfTheStore, bool classPart = true)
    {
        var item = new ContentItem
        {
            ContentItemId = ClassItemId,
            ContentType = ClassesConstants.ContentTypes.Class,
        };

        if (classPart)
        {
            item.Apply(new ClassPart());
        }

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync(ClassItemId, It.IsAny<VersionOptions>()))
            .ReturnsAsync(item);

        var courseStructure = new Mock<ICourseStructureService>();
        courseStructure
            .Setup(service => service.GetCourseId(It.IsAny<ContentItem>()))
            .Returns(CourseId);

        // The store is asked for one learner, but a wrong answer must never hand another learner's enrollment over.
        var enrollments = new Mock<IEnrollmentStore>();
        enrollments
            .Setup(store => store.ListByCourseForUserAsync(It.IsAny<string>(), CourseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enrollmentsOfTheStore);

        return new ClassLearnerResolver(contentManager.Object, courseStructure.Object, enrollments.Object);
    }

    private static ClassRegistrationController CreateController(string userId, IReadOnlyList<Enrollment> enrollments, Mock<IClassRegistrationService> registrations, Mock<INotifier> notifier)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                user.Identity?.IsAuthenticated == true && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == LearningPermissions.AccessLearning.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var localizer = new Mock<IHtmlLocalizer<ClassRegistrationController>>();
        localizer
            .Setup(service => service[It.IsAny<string>()])
            .Returns<string>(name => new LocalizedHtmlString(name, name));
        localizer
            .Setup(service => service[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((name, arguments) => new LocalizedHtmlString(name, name, false, arguments));

        var url = new Mock<IUrlHelper>();
        url
            .Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns($"/learning/courses/{CourseId}/play/{ClassItemId}");
        url
            .Setup(helper => helper.Content(It.IsAny<string>()))
            .Returns<string>(path => path.TrimStart('~'));

        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test");

        return new ClassRegistrationController(authorization.Object, CreateResolver(enrollments), registrations.Object, notifier.Object, localizer.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            },
            Url = url.Object,
        };
    }

    private static Enrollment Enrollment(string id, string userId, EnrollmentStatus status)
        => new()
        {
            ItemId = id,
            UserId = userId,
            CourseId = CourseId,
            Status = status,
        };
}
