using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7: a learner hands coursework in only under their own active enrollment; the file rules refuse a
/// wrong type, a large file or too many files and leave no file behind; work after the due date is late; a hand-in
/// again is allowed only when the coursework takes it or a grader returned the work, and numbers the attempts; a grade
/// reaches the course grade; a regrade needs a reason and keeps the earlier grade.
/// </summary>
public sealed class SubmissionServiceTests
{
    private const string Course = "course1";
    private const string Work = "essay1";

    [Fact]
    public async Task Submit_ALearnerWhoIsNotEnrolled_IsRefused_EvenWhenAnotherLearnerIs()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ben", Course);

        // Act
        var result = await h.SubmitAsync("ana", Work, "My answer", ("essay.pdf", 100));

        // Assert
        Assert.Equal(SubmissionErrors.NotEnrolled, result.Error);
        Assert.Empty(h.Files.Stored);
    }

    [Fact]
    public async Task Submit_KeepsTheTextAndFiles_UnderTheLearnersOwnEnrollment()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ben", Course);
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        var result = await h.SubmitAsync("ana", Work, "My answer", ("essay.pdf", 100));
        var stored = await h.FindAsync(result.Submission?.ItemId);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ana.ItemId, stored.EnrollmentId);
        Assert.Equal("ana", stored.UserId);
        Assert.Equal(Course, stored.CourseId);
        Assert.Equal("My answer", stored.Text);
        Assert.Equal(1, stored.Attempt);
        Assert.Equal(SubmissionStatus.Submitted, stored.Status);
        Assert.Equal("essay.pdf", Assert.Single(stored.Files).FileName);
        Assert.Single(h.Files.Stored);
    }

    [Theory]
    [InlineData("essay.exe", 100, 1, SubmissionErrors.ExtensionNotAllowed)]
    [InlineData("essay.pdf", 3 * 1024 * 1024, 1, SubmissionErrors.FileTooLarge)]
    [InlineData("essay.pdf", 100, 3, SubmissionErrors.TooManyFiles)]
    public async Task Submit_BreakingAFileRule_IsRefused_AndNoFileIsKept(string name, int bytes, int count, string error)
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var files = Enumerable.Range(0, count).Select(index => (index == 0 ? name : $"extra{index}.pdf", bytes)).ToArray();

        // Act
        var result = await h.SubmitAsync("ana", Work, null, files);

        // Assert
        Assert.Equal(error, result.Error);
        Assert.Empty(h.Files.Stored);
    }

    [Fact]
    public async Task Submit_AFileTheStoreRefuses_RemovesTheFilesAlreadyKept_AndSavesNothing()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        h.Files.Refused.Add("infected.pdf");

        // Act
        var result = await h.SubmitAsync("ana", Work, null, ("fine.pdf", 100), ("infected.pdf", 100));
        var submissions = await h.RunAsync(services => Service(services).ListForEnrollmentAsync(ana.ItemId, Work));

        // Assert
        Assert.Equal(SubmissionErrors.FileRefused, result.Error);
        Assert.Empty(h.Files.Stored);
        Assert.Single(h.Files.Deleted);
        Assert.Empty(submissions);
    }

    [Fact]
    public async Task Submit_TextOnlyCoursework_RefusesFiles_AndNeedsText()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.SubmissionType = CourseworkSubmissionType.Text);
        await h.EnrollAsync("ana", Course);

        // Act
        var withFile = await h.SubmitAsync("ana", Work, "Answer", ("essay.pdf", 100));
        var empty = await h.SubmitAsync("ana", Work, "  ");

        // Assert
        Assert.Equal(SubmissionErrors.FilesNotAllowed, withFile.Error);
        Assert.Equal(SubmissionErrors.TextRequired, empty.Error);
    }

    [Fact]
    public async Task Submit_AfterTheDueDate_IsMarkedLate_AndBeforeItIsNot()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.AllowResubmission = true);
        await h.EnrollAsync("ana", Course);
        h.Courses.Get(Work).Alter<CourseworkPart>(part => part.DueUtc = LearningTestHarness.Now.AddHours(1));

        // Act
        var onTime = await h.SubmitAsync("ana", Work, "First");
        h.Clock.UtcNow = LearningTestHarness.Now.AddHours(2);
        var late = await h.SubmitAsync("ana", Work, "Second");

        // Assert
        Assert.True(onTime.Succeeded, onTime.Error);
        Assert.False(onTime.Submission.Late);
        Assert.True(late.Succeeded, late.Error);
        Assert.True(late.Submission.Late);
    }

    [Fact]
    public async Task Submit_Again_IsRefused_UnlessTheCourseworkTakesAResubmission()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        await h.SubmitAsync("ana", Work, "First");

        // Act
        var again = await h.SubmitAsync("ana", Work, "Second");

        // Assert
        Assert.Equal(SubmissionErrors.AlreadySubmitted, again.Error);
    }

    [Fact]
    public async Task Submit_Again_WhenAllowed_IsTheNextAttempt_AndReplacesTheEarlierOne()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.AllowResubmission = true);
        await h.EnrollAsync("ana", Course);
        var first = await h.SubmitAsync("ana", Work, "First");

        // Act
        var second = await h.SubmitAsync("ana", Work, "Second");
        var earlier = await h.FindAsync(first.Submission.ItemId);
        var waiting = await h.RunAsync(services => Service(services).ListWaitingAsync(null, 0, 10));

        // Assert
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(2, second.Submission.Attempt);
        Assert.False(earlier.Current);
        Assert.Equal(second.Submission.ItemId, Assert.Single(waiting.Items).ItemId);
    }

    [Fact]
    public async Task Submit_AfterGrading_IsRefused_UntilAGraderReturnsTheWork()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.AllowResubmission = true);
        await h.EnrollAsync("ana", Course);
        var first = await h.SubmitAsync("ana", Work, "First");
        await h.GradeAsync(first.Submission.ItemId, "grader", points: 40);

        // Act
        var refused = await h.SubmitAsync("ana", Work, "Second");
        var returned = await h.ReturnAsync(first.Submission.ItemId, "grader");
        var accepted = await h.SubmitAsync("ana", Work, "Second");

        // Assert
        Assert.Equal(SubmissionErrors.AlreadyGraded, refused.Error);
        Assert.True(returned.Succeeded, returned.Error);
        Assert.Equal(SubmissionStatus.Returned, returned.Submission.Status);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(2, accepted.Submission.Attempt);
    }

    [Fact]
    public async Task Submit_CompletesTheItem_WhenItIsCompletedBySubmitting()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);

        // Act
        await h.SubmitAsync("ana", Work, "Answer");
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.Equal(CourseItemState.Completed, progress.State);
    }

    [Fact]
    public async Task Grade_ReachesTheCourseGrade_ThroughTheLearnersProgress()
    {
        // Arrange
        await using var h = await CreateAsync();
        h.AddCoursework(Course, "essay2");
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        var before = await h.FindEnrollmentAsync(ana.ItemId);

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        var after = await h.FindEnrollmentAsync(ana.ItemId);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(80m, graded.Submission.Score);
        Assert.Equal(80m, progress.Score);
        Assert.Equal(0m, before.Grade ?? 0m);
        Assert.Equal(40m, after.Grade);
    }

    [Fact]
    public async Task Grade_AgainstARubric_ScoresTheChosenLevels()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.RubricId = "rubric1");
        h.AddRubric("rubric1", ("argument", [("weak", 1m), ("strong", 4m)]), ("sources", [("none", 0m), ("many", 6m)]));
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var incomplete = await h.GradeAsync(submitted.Submission.ItemId, "grader", choices: new Dictionary<string, string> { ["argument"] = "strong" });
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", choices: new Dictionary<string, string> { ["argument"] = "strong", ["sources"] = "none" });

        // Assert
        Assert.Equal(SubmissionErrors.InvalidScore, incomplete.Error);
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(4m, graded.Submission.Points);
        Assert.Equal(10m, graded.Submission.MaxPoints);
        Assert.Equal(40m, graded.Submission.Score);
        Assert.Equal("rubric1", graded.Submission.RubricId);
    }

    [Fact]
    public async Task Grade_PointsOutOfRange_AreRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var tooMany = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 101);
        var none = await h.GradeAsync(submitted.Submission.ItemId, "grader");

        // Assert
        Assert.Equal(SubmissionErrors.InvalidScore, tooMany.Error);
        Assert.Equal(SubmissionErrors.InvalidScore, none.Error);
    }

    [Fact]
    public async Task Regrade_NeedsAReason_AndKeepsTheEarlierGrade()
    {
        // Arrange
        await using var h = await CreateAsync();
        h.AddCoursework(Course, "essay2", weight: 0);
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 50, feedback: "Thin");

        // Act
        var withoutReason = await h.GradeAsync(submitted.Submission.ItemId, "grader2", points: 90);
        var regraded = await h.GradeAsync(submitted.Submission.ItemId, "grader2", points: 90, reason: "Missed the appendix");
        var stored = await h.FindAsync(submitted.Submission.ItemId);
        var enrollment = await h.FindEnrollmentAsync(ana.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.ReasonRequired, withoutReason.Error);
        Assert.True(regraded.Succeeded, regraded.Error);
        Assert.Equal(90m, stored.Score);
        Assert.Equal("grader2", stored.GraderId);
        var earlier = Assert.Single(stored.History);
        Assert.Equal(50m, earlier.Score);
        Assert.Equal("grader", earlier.GraderId);
        Assert.Equal("Thin", earlier.Feedback);
        Assert.Equal("grader2", earlier.ReplacedBy);
        Assert.Equal("Missed the appendix", earlier.Reason);
        Assert.Equal(90m, enrollment.Grade);
    }

    [Fact]
    public async Task Grade_OnesOwnSubmission_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "ana", points: 100);
        var returned = await h.ReturnAsync(submitted.Submission.ItemId, "ana");

        // Assert
        Assert.Equal(SubmissionErrors.OwnSubmission, graded.Error);
        Assert.Equal(SubmissionErrors.OwnSubmission, returned.Error);
    }

    [Fact]
    public async Task Return_TakesTheGradeOutOfTheCourseGrade_UntilTheWorkIsGradedAgain()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        h.AddCoursework(Course, "essay2");
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60);

        // Act
        await h.ReturnAsync(submitted.Submission.ItemId, "grader");
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.Null(progress.Score);
        Assert.NotEqual(CourseItemState.Completed, progress.State);
    }

    private static async Task<CourseworkTestHarness> CreateAsync(Action<CourseworkPart> configure = null)
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse(Course);

        // A lesson nobody opens keeps the course open: a completed enrollment is a record and takes no more work.
        h.Courses.AddItem(Course, "lesson1");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = CourseworkSubmissionType.TextAndFiles;
            part.AllowedExtensions = [".pdf", ".docx"];
            part.MaxFileSizeMb = 2;
            part.MaxFiles = 2;
            part.MaxPoints = 100;
            configure?.Invoke(part);
        });

        return h;
    }

    private static ISubmissionService Service(IServiceProvider services)
        => (ISubmissionService)services.GetService(typeof(ISubmissionService));
}
