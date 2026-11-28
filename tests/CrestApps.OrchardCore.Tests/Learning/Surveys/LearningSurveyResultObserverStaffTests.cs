using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Round 12 M4: a grade, a regrade, a void or a delete a member of the course staff makes reaches the gradebook on the
/// staff path (<see cref="CourseItemResult.RecordedBy"/>), so it is recorded after the learner's access window ended, as
/// the grade of an essay submitted on the last day and graded a week later must be. A completion the learner made stays
/// on the learner path.
/// </summary>
public sealed class LearningSurveyResultObserverStaffTests
{
    [Fact]
    public async Task R12M4_AStaffGrade_IsRecordedOnTheStaffPath()
    {
        // Arrange
        var (observer, recorded) = Observer();

        // Act
        await observer.GradedAsync(Event(actorId: "grader1"));

        // Assert
        var result = Assert.Single(recorded);
        Assert.Equal("grader1", result.RecordedBy);
    }

    [Theory]
    [InlineData("score")]
    [InlineData("void")]
    [InlineData("delete")]
    public async Task R12M4_AStaffRegradeVoidOrDelete_IsRecordedOnTheStaffPath(string change)
    {
        // Arrange
        var (observer, recorded) = Observer();
        var context = Event(actorId: "grader1");

        // Act
        await (change switch
        {
            "score" => observer.ScoreChangedAsync(context),
            "void" => observer.VoidedAsync(context),
            _ => observer.DeletedAsync(context),
        });

        // Assert
        var result = Assert.Single(recorded);
        Assert.Equal("grader1", result.RecordedBy);
    }

    [Fact]
    public async Task R12M4_AWorkflowGrade_IsRecordedOnTheStaffPath_AsTheSystem()
    {
        // Arrange
        var (observer, recorded) = Observer();
        var context = Event(actorId: null);
        context.CausedByWorkflow = true;

        // Act
        await observer.GradedAsync(context);

        // Assert
        var result = Assert.Single(recorded);
        Assert.False(string.IsNullOrEmpty(result.RecordedBy));
    }

    [Fact]
    public async Task R12M4_TheLearnersOwnCompletion_StaysOnTheLearnerPath()
    {
        // Arrange
        var (observer, recorded) = Observer();

        // Act
        await observer.CompletedAsync(Event(actorId: null));

        // Assert
        var result = Assert.Single(recorded);
        Assert.Null(result.RecordedBy);
    }

    private static SurveyResponseEventContext Event(string actorId)
        => new()
        {
            Survey = new Survey { ItemId = "survey1", Owner = LearningSurveyContexts.Owner("quiz1"), Access = new SurveyAccessSettings() },
            Version = new SurveyVersion { ItemId = "v1", Definition = new SurveyDefinition() },
            Response = new SurveyResponse
            {
                ItemId = "response1",
                SurveyId = "survey1",
                Status = SurveyResponseStatus.Completed,
                Context = LearningSurveyContexts.Enrollment("enrollment1"),
            },
            ActorId = actorId,
        };

    private static (LearningSurveyResultObserver Observer, List<CourseItemResult> Recorded) Observer()
    {
        var item = new ContentItem { ContentItemId = "quiz1", ContentType = "Quiz" };
        var course = new ContentItem { ContentItemId = "course1", ContentType = "Course" };
        var enrollment = new Enrollment { ItemId = "enrollment1", CourseId = "course1", UserId = "learner1", Status = EnrollmentStatus.Active };
        var outline = new CourseOutline
        {
            Course = course,
            Entries = [new CourseOutlineEntry { Item = item, Part = new CourseItemPart { Kind = "Quiz" } }],
        };

        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys.Setup(service => service.FindOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync(item);
        learningSurveys.Setup(service => service.ListEnrollmentResponsesAsync("survey1", "enrollment1", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var structure = new Mock<ICourseStructureService>();
        structure.Setup(service => service.GetCourseId(item)).Returns("course1");
        structure.Setup(service => service.GetCourseAsync("course1")).ReturnsAsync(course);
        structure.Setup(service => service.GetOutlineAsync(course)).ReturnsAsync(outline);

        var enrollments = new Mock<IEnrollmentStore>();
        enrollments.Setup(store => store.FindByIdAsync("enrollment1", It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);

        var recorded = new List<CourseItemResult>();
        var progress = new Mock<ILearnerProgressService>();
        progress
            .Setup(service => service.RecordResultAsync(It.IsAny<Enrollment>(), It.IsAny<CourseItemResult>(), It.IsAny<CancellationToken>()))
            .Callback<Enrollment, CourseItemResult, CancellationToken>((_, result, _) => recorded.Add(result))
            .ReturnsAsync(new ProgressUpdate { Enrollment = enrollment });

        var provider = new Mock<ILearningSurveyItemResultProvider>();
        provider.SetupGet(candidate => candidate.Kind).Returns("Quiz");
        provider
            .Setup(candidate => candidate.Build(It.IsAny<ContentItem>(), It.IsAny<Enrollment>(), It.IsAny<SurveyDefinition>(), It.IsAny<IReadOnlyList<SurveyResponse>>()))
            .Returns(() => new CourseItemResult { ItemId = "quiz1", Score = 80m, Graded = true, Submitted = true });

        var observer = new LearningSurveyResultObserver(
            learningSurveys.Object,
            structure.Object,
            enrollments.Object,
            progress.Object,
            [provider.Object],
            new HttpContextAccessor(),
            Mock.Of<ISiteService>(),
            Mock.Of<IClock>(),
            NullLogger<LearningSurveyResultObserver>.Instance,
            new Mock<IServiceProvider>().Object);

        return (observer, recorded);
    }
}
