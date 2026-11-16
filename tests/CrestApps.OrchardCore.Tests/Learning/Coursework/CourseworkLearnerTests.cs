using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Controllers;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7, the coursework panel: the learner's enrollment is found from the coursework item and the
/// signed-in user, never from the form, and another learner's enrollment is never used; a hand-in is always made for
/// the signed-in learner.
/// </summary>
public sealed class CourseworkLearnerTests
{
    private const string WorkId = "work1";
    private const string CourseId = "course1";

    [Fact]
    public async Task Resolve_NeverUsesAnotherLearnersEnrollment()
    {
        // Arrange
        var resolver = CreateResolver([Enrollment("e-ben", "ben", EnrollmentStatus.Active)]);

        // Act
        var learner = await resolver.ResolveAsync("ana", WorkId);

        // Assert
        Assert.NotNull(learner);
        Assert.Equal(CourseId, learner.CourseId);
        Assert.Null(learner.Enrollment);
    }

    [Fact]
    public async Task Resolve_UsesTheLearnersOpenEnrollment_ElseTheNewestThatEnded()
    {
        // Arrange
        var open = CreateResolver([Enrollment("e-done", "ana", EnrollmentStatus.Completed), Enrollment("e-open", "ana", EnrollmentStatus.Active)]);
        var ended = CreateResolver([Enrollment("e-done", "ana", EnrollmentStatus.Completed), Enrollment("e-ben", "ben", EnrollmentStatus.Active)]);

        // Act
        var withOpen = await open.ResolveAsync("ana", WorkId);
        var withEnded = await ended.ResolveAsync("ana", WorkId);

        // Assert
        Assert.Equal("e-open", withOpen.Enrollment.ItemId);
        Assert.Equal("e-done", withEnded.Enrollment.ItemId);
    }

    [Fact]
    public async Task Resolve_AnItemThatIsNotCoursework_IsNotFound()
    {
        // Arrange
        var resolver = CreateResolver([Enrollment("e-ana", "ana", EnrollmentStatus.Active)], courseworkPart: false);

        // Act
        var learner = await resolver.ResolveAsync("ana", WorkId);

        // Assert
        Assert.Null(learner);
    }

    [Fact]
    public async Task Submit_IsMadeForTheSignedInLearner_WhateverElseIsEnrolled()
    {
        // Arrange
        var submissions = new Mock<ISubmissionService>();
        submissions
            .Setup(service => service.SubmitAsync(It.IsAny<SubmitCourseworkRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionResult { Submission = new Submission { ItemId = "s1" } });
        var controller = CreateController("ana", [Enrollment("e-ben", "ben", EnrollmentStatus.Active), Enrollment("e-ana", "ana", EnrollmentStatus.Active)], submissions);

        // Act
        var result = await controller.Submit(WorkId, "My answer", []);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        submissions.Verify(service => service.SubmitAsync(It.Is<SubmitCourseworkRequest>(request => request.UserId == "ana" && request.CourseworkId == WorkId && request.Text == "My answer"), It.IsAny<CancellationToken>()), Times.Once);
        submissions.Verify(service => service.SubmitAsync(It.Is<SubmitCourseworkRequest>(request => request.UserId != "ana"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WithoutASignedInLearner_IsChallenged_AndNothingIsHandedIn()
    {
        // Arrange
        var submissions = new Mock<ISubmissionService>();
        var controller = CreateController(null, [Enrollment("e-ana", "ana", EnrollmentStatus.Active)], submissions);

        // Act
        var result = await controller.Submit(WorkId, "My answer", []);

        // Assert
        Assert.IsType<ChallengeResult>(result);
        Assert.Empty(submissions.Invocations);
    }

    [Fact]
    public async Task Submit_AfterTheCloseDate_SaysItClosed_WithoutPromisingAnExtension()
    {
        // Arrange: there is no per-learner extension, so the message must not send the learner to ask for one.
        var submissions = new Mock<ISubmissionService>();
        submissions
            .Setup(service => service.SubmitAsync(It.IsAny<SubmitCourseworkRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmissionResult.Fail(SubmissionErrors.Closed));
        var notifier = new Mock<INotifier>();
        var shown = new List<string>();
        notifier
            .Setup(service => service.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback<NotifyType, LocalizedHtmlString>((_, message) => shown.Add(message.Value))
            .Returns(ValueTask.CompletedTask);
        var controller = CreateController("ana", [Enrollment("e-ana", "ana", EnrollmentStatus.Active)], submissions, notifier.Object);

        // Act
        await controller.Submit(WorkId, "My answer", []);

        // Assert
        var message = Assert.Single(shown);
        Assert.Contains("closed", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructor", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("more time", message, StringComparison.OrdinalIgnoreCase);
    }

    private static CourseworkLearnerResolver CreateResolver(IReadOnlyList<Enrollment> enrollmentsOfTheStore, bool courseworkPart = true)
    {
        var item = new ContentItem
        {
            ContentItemId = WorkId,
            ContentType = CourseworkConstants.ContentTypes.Coursework,
        };

        if (courseworkPart)
        {
            item.Apply(new CourseworkPart());
        }

        var content = new Mock<ICourseworkContentSource>();
        content
            .Setup(source => source.GetPublishedAsync(WorkId))
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

        return new CourseworkLearnerResolver(content.Object, courseStructure.Object, enrollments.Object);
    }

    private static CourseworkSubmissionController CreateController(string userId, IReadOnlyList<Enrollment> enrollments, Mock<ISubmissionService> submissions, INotifier notifier = null)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                user.Identity?.IsAuthenticated == true && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == LearningPermissions.AccessLearning.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var localizer = new Mock<IHtmlLocalizer<CourseworkSubmissionController>>();
        localizer
            .Setup(service => service[It.IsAny<string>()])
            .Returns<string>(name => new LocalizedHtmlString(name, name));

        var url = new Mock<IUrlHelper>();
        url
            .Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns($"/learning/courses/{CourseId}/play/{WorkId}");

        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test");

        return new CourseworkSubmissionController(authorization.Object, CreateResolver(enrollments), submissions.Object, notifier ?? new Mock<INotifier>().Object, localizer.Object)
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
