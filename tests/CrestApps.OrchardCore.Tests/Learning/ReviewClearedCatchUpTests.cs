using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A completion that did not support issuing while its enrollment was under review catches up when a person clears the
/// review: the enrollment service tells the observers, and the course credential, course education credits, learning
/// paths and certification paths run their completion checks again. Each check is idempotent, so a completion that was
/// already awarded is not awarded twice.
/// </summary>
public sealed class ReviewClearedCatchUpTests
{
    [Fact]
    public async Task ClearingAReview_TellsTheObservers_Once()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "exam", completionMode: CompletionModes.Passed);
        var enrollment = (await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "learner",
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-learner",
        }))).Enrollment;
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", Score = 90, Passed = true, Submitted = true, Graded = true, Attempts = 1 });
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", ResetScore = true });

        // Act: the review is cleared, then cleared again (a second click, or a stale page).
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ClearReviewAsync(enrollment.ItemId, "manager", "Retake passed"));
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ClearReviewAsync(enrollment.ItemId, "manager", "Retake passed"));

        // Assert
        Assert.Single(observer.Events, name => name == "ReviewCleared");
    }

    [Fact]
    public async Task TheCourseCredential_IsTriggeredAgain_WhenTheReviewOfACompletionIsCleared()
    {
        // Arrange
        var catalog = new Mock<ICredentialDefinitionCatalog>();
        catalog.Setup(value => value.ListByRuleAsync(CredentialsConstants.RuleKinds.CourseCompleted, "course1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CredentialDefinition { DefinitionId = "badge1" }]);
        var queued = new List<CredentialAwardRequest>();
        var queue = new Mock<ICredentialAwardQueue>();
        queue.Setup(value => value.Enqueue(It.IsAny<CredentialAwardRequest>())).Callback((CredentialAwardRequest request) => queued.Add(request));
        var observer = new CredentialEnrollmentObserver(new CredentialRuleTrigger(catalog.Object, queue.Object, NullLogger<CredentialRuleTrigger>.Instance));

        // Act
        await ((IEnrollmentObserver)observer).ReviewClearedAsync(Completed(), TestContext.Current.CancellationToken);

        // Assert: the same request the completion would have made, which the award service keeps to one award.
        var request = Assert.Single(queued);
        Assert.Equal("badge1", request.DefinitionId);
        Assert.Equal("learner", request.UserId);
        Assert.Equal("e1", request.SourceId);
    }

    [Fact]
    public async Task TheCourseCredential_IsNotTriggered_ForAClearedReviewOfAnEnrollmentThatIsNotCompleted()
    {
        var catalog = new Mock<ICredentialDefinitionCatalog>();
        var queue = new Mock<ICredentialAwardQueue>();
        var observer = new CredentialEnrollmentObserver(new CredentialRuleTrigger(catalog.Object, queue.Object, NullLogger<CredentialRuleTrigger>.Instance));
        var enrollment = Completed();
        enrollment.Status = EnrollmentStatus.Withdrawn;

        await ((IEnrollmentObserver)observer).ReviewClearedAsync(enrollment, TestContext.Current.CancellationToken);

        queue.Verify(value => value.Enqueue(It.IsAny<CredentialAwardRequest>()), Times.Never);
    }

    [Fact]
    public async Task LearningPaths_AreCheckedAgain_WhenTheReviewOfACompletionIsCleared()
    {
        // Arrange
        var paths = new Mock<ILearningPathService>();
        var services = new ServiceCollection().AddSingleton(paths.Object).BuildServiceProvider();
        var observer = new ProgramsEnrollmentObserver(services);

        // Act
        await ((IEnrollmentObserver)observer).ReviewClearedAsync(Completed(), TestContext.Current.CancellationToken);

        // Assert
        paths.Verify(value => value.EvaluateCourseAsync("learner", "course1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CertificationPaths_AreCheckedAgain_WhenTheReviewOfACompletionIsCleared()
    {
        // Arrange
        var paths = new Mock<ICertificationPathService>();
        paths.Setup(value => value.ListByTargetAsync("course1", It.IsAny<CancellationToken>())).ReturnsAsync([new CertificationPath { ItemId = "path1" }]);
        var services = new ServiceCollection().AddSingleton(paths.Object).BuildServiceProvider();
        var observer = new CertificationEnrollmentObserver(services, NullLogger<CertificationEnrollmentObserver>.Instance);

        // Act
        await ((IEnrollmentObserver)observer).ReviewClearedAsync(Completed(), TestContext.Current.CancellationToken);

        // Assert
        paths.Verify(value => value.EvaluateAsync("path1", "learner", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EducationCredits_AreAwardedAgain_WhenTheReviewOfACompletionIsCleared()
    {
        // Arrange: no content manager, so the observer stops after deciding the completion supports issuing; resolving
        // it proves the observer went past that check.
        var resolved = new List<Type>();
        var services = new Mock<IServiceProvider>();
        services.Setup(value => value.GetService(It.IsAny<Type>())).Returns((Type type) =>
        {
            resolved.Add(type);

            return null;
        });
        var observer = new EducationCreditEnrollmentObserver(services.Object, NullLogger<EducationCreditEnrollmentObserver>.Instance);

        // Act
        await ((IEnrollmentObserver)observer).ReviewClearedAsync(Completed(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(typeof(global::OrchardCore.ContentManagement.IContentManager), resolved);
    }

    private static Enrollment Completed()
        => new()
        {
            ItemId = "e1",
            UserId = "learner",
            CourseId = "course1",
            Status = EnrollmentStatus.Completed,
            Passed = true,
            Review = new EnrollmentReview { Reason = LearningReviewReasons.ResultChangedAfterCompletion, ClearedUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc) },
        };

    private static Task<ProgressUpdate> Result(CalendarTestHarness harness, string enrollmentId, CourseItemResult result)
        => harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId);

            return await services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(enrollment, result);
        });
}
