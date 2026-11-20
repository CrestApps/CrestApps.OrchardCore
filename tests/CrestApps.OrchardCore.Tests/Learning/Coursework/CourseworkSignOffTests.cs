using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Remaining work section 3, item 12 (Moodle assignments): a grade-only sign-off with nothing handed in, work that
/// completes on hand-in and is still graded, and up to 20 files per submission (configurable).
/// </summary>
public sealed class CourseworkSignOffTests
{
    private const string Course = "course1";
    private const string Work = "signoff1";

    [Fact]
    public async Task SignOff_ALearnerCannotHandAnythingIn()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        await h.EnrollAsync("ana", Course);

        // Act
        var result = await h.SubmitAsync("ana", Work, "My answer");

        // Assert
        Assert.Equal(SubmissionErrors.SignOffOnly, result.Error);
    }

    [Fact]
    public async Task SignOff_AGraderOpensAnEmptyRecord_ThatDoesNotCompleteTheItem()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var started = await StartAsync(h, ana.ItemId, "grader");
        var again = await StartAsync(h, ana.ItemId, "grader");
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.True(started.Succeeded, started.Error);
        Assert.True(started.Submission.SignOff);
        Assert.Null(started.Submission.Text);
        Assert.Empty(started.Submission.Files);
        Assert.Equal(SubmissionStatus.Submitted, started.Submission.Status);
        Assert.Equal(started.Submission.ItemId, again.Submission.ItemId);
        Assert.NotEqual(CourseItemState.Completed, progress?.State);
    }

    [Fact]
    public async Task SignOff_TheGrade_CompletesTheItem_AndReachesTheCourseGrade()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        var ana = await h.EnrollAsync("ana", Course);
        var started = await StartAsync(h, ana.ItemId, "grader");

        // Act
        var graded = await h.GradeAsync(started.Submission.ItemId, "grader", points: 100);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(CourseItemState.Completed, progress.State);
        Assert.Equal(100m, progress.Score);
    }

    [Fact]
    public async Task SignOff_ReturnedByAGrader_OpensANewAttemptToGrade()
    {
        // Arrange: found in the browser pass. A returned sign-off can never be handed in again by the learner, so the
        // next sign-off must not reopen the returned record, which cannot be graded.
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        var ana = await h.EnrollAsync("ana", Course);
        var first = await StartAsync(h, ana.ItemId, "grader");
        await h.ReturnAsync(first.Submission.ItemId, "grader");

        // Act
        var again = await StartAsync(h, ana.ItemId, "grader");
        var graded = await h.GradeAsync(again.Submission.ItemId, "grader", points: 80);

        // Assert
        Assert.True(again.Succeeded, again.Error);
        Assert.NotEqual(first.Submission.ItemId, again.Submission.ItemId);
        Assert.Equal(2, again.Submission.Attempt);
        Assert.True(graded.Succeeded, graded.Error);
    }

    [Fact]
    public async Task SignOff_OnCourseworkThatTakesHandIns_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.TextAndFiles);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var result = await StartAsync(h, ana.ItemId, "grader");

        // Assert
        Assert.Equal(SubmissionErrors.NotSignOff, result.Error);
    }

    [Fact]
    public async Task SignOff_ForAnotherCoursesEnrollment_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        h.Courses.AddCourse("course2");
        h.Courses.AddItem("course2", "lesson2");
        var other = await h.EnrollAsync("ana", "course2");

        // Act
        var result = await StartAsync(h, other.ItemId, "grader");

        // Assert
        Assert.Equal(SubmissionErrors.NotEnrolled, result.Error);
    }

    [Fact]
    public async Task SignOff_OfOnesOwnWork_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.SignOff);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var result = await StartAsync(h, ana.ItemId, "ana");

        // Assert
        Assert.Equal(SubmissionErrors.OwnSubmission, result.Error);
    }

    [Fact]
    public void TheCourseworkKind_AllowsCompletingOnHandIn_WhenTheWorkIsGraded()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningCourseworkCore();

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<IOptions<CourseItemKindOptions>>().Value.Find("Coursework");

        // Assert
        Assert.True(kind.Allows(CompletionModes.Submitted, gradable: true));
        Assert.True(kind.Allows(CompletionModes.Graded, gradable: true));
        Assert.Equal(CompletionModes.Graded, kind.DefaultCompletionMode);
    }

    [Fact]
    public async Task CompletesOnHandIn_AndTheLaterGrade_StillCounts()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.TextAndFiles);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        var handedIn = await h.Inner.ProgressAsync(ana.ItemId, Work);
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 70);
        var graded = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.Equal(CourseItemState.Completed, handedIn.State);
        Assert.Equal(70m, graded.Score);
    }

    [Fact]
    public void Options_TakeTwentyFilesBreakingNoRequestLimit_ByDefault()
    {
        // Arrange
        var options = new CourseworkOptions();

        // Act
        var requestBytes = options.MaxRequestBytes;

        // Assert
        Assert.Equal(20, options.MaxFilesPerSubmission);
        Assert.True(requestBytes >= 20L * options.MaxFileSizeMb * 1024 * 1024);
    }

    [Fact]
    public async Task TwentyFiles_AreTaken_WhenTheCourseworkAllowsTwenty_AndTheTwentyFirstIsNot()
    {
        // Arrange
        await using var h = await CreateAsync(CourseworkSubmissionType.Files, part => part.MaxFiles = 20);
        await h.EnrollAsync("ana", Course);
        await h.EnrollAsync("ben", Course);
        var twenty = Enumerable.Range(1, 20).Select(index => ($"page{index}.pdf", 10)).ToArray();
        var twentyOne = Enumerable.Range(1, 21).Select(index => ($"page{index}.pdf", 10)).ToArray();

        // Act
        var accepted = await h.SubmitAsync("ana", Work, null, twenty);
        var refused = await h.SubmitAsync("ben", Work, null, twentyOne);

        // Assert
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(20, accepted.Submission.Files.Count);
        Assert.Equal(SubmissionErrors.TooManyFiles, refused.Error);
    }

    private static Task<SubmissionResult> StartAsync(CourseworkTestHarness h, string enrollmentId, string actor)
        => h.RunAsync(services => services.GetRequiredService<ISubmissionService>().StartSignOffAsync(new SignOffRequest
        {
            CourseworkId = Work,
            EnrollmentId = enrollmentId,
            Actor = actor,
        }));

    private static async Task<CourseworkTestHarness> CreateAsync(CourseworkSubmissionType type, Action<CourseworkPart> configure = null)
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse(Course);
        h.Courses.AddItem(Course, "lesson1");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = type;
            part.AllowedExtensions = [".pdf"];
            part.MaxFileSizeMb = 2;
            part.MaxFiles = 2;
            part.MaxPoints = 100;
            configure?.Invoke(part);
        }, completionMode: type == CourseworkSubmissionType.SignOff ? CompletionModes.Graded : CompletionModes.Submitted);

        return h;
    }
}
