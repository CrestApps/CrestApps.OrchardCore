using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7 (section 7.6): late work loses a percentage a day (a part of a day counts as a day) up to a
/// cap, the final score is rounded to two decimals and is what counts in the course grade, a regrade keeps the days late
/// and the penalty, and coursework stops taking work a number of whole days after the due date.
/// </summary>
public sealed class CourseworkLatePolicyTests
{
    private const string Course = "course1";
    private const string Work = "essay1";

    private static readonly DateTime _due = LearningTestHarness.Now;

    [Theory]
    [InlineData(-60, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(24 * 60, 1)]
    [InlineData(24 * 60 + 1, 2)]
    [InlineData(3 * 24 * 60, 3)]
    public void DaysLate_CountsAPartOfADayAsADay(int minutesAfterDue, int expected)
    {
        // Act
        var days = CourseworkLatePolicy.GetDaysLate(_due, _due.AddMinutes(minutesAfterDue));

        // Assert
        Assert.Equal(expected, days);
    }

    [Fact]
    public void DaysLate_WithoutADueDate_IsZero()
        => Assert.Equal(0, CourseworkLatePolicy.GetDaysLate(null, _due.AddDays(30)));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 25)]
    [InlineData(40, 25)]
    public void Penalty_IsThePercentADayTimesTheDays_UpToTheCap(int days, decimal expected)
    {
        // Arrange
        var part = new CourseworkPart { LatePenaltyPercentPerDay = 10, MaxLatePenaltyPercent = 25 };

        // Act
        var penalty = CourseworkLatePolicy.GetPenaltyPercent(part, days);

        // Assert
        Assert.Equal(expected, penalty);
    }

    [Fact]
    public void Penalty_IsZero_WithoutAPercentADay()
        => Assert.Equal(0m, CourseworkLatePolicy.GetPenaltyPercent(new CourseworkPart { LatePenaltyPercentPerDay = 0, MaxLatePenaltyPercent = 100 }, 9));

    [Theory]
    [InlineData(87.35, 15, 74.25)]
    [InlineData(80, 0, 80)]
    [InlineData(100, 100, 0)]
    [InlineData(33.33, 10, 30)]
    public void FinalScore_IsTheRawScoreLessThePenalty_RoundedToTwoDecimals(decimal raw, decimal penalty, decimal expected)
        => Assert.Equal(expected, CourseworkLatePolicy.Apply(raw, penalty));

    [Fact]
    public void Close_IsWholeDaysAfterTheDueDate_AndNeverWithoutADueDateOrADayCount()
    {
        // Arrange
        var part = new CourseworkPart { DueUtc = _due, CloseAfterDays = 2 };

        // Act and assert
        Assert.Equal(_due.AddDays(2), CourseworkLatePolicy.GetCloseUtc(part));
        Assert.False(CourseworkLatePolicy.IsClosed(part, _due.AddDays(2)));
        Assert.True(CourseworkLatePolicy.IsClosed(part, _due.AddDays(2).AddSeconds(1)));
        Assert.True(CourseworkLatePolicy.IsClosed(new CourseworkPart { DueUtc = _due, CloseAfterDays = 0 }, _due.AddSeconds(1)));
        Assert.False(CourseworkLatePolicy.IsClosed(new CourseworkPart { DueUtc = _due }, _due.AddYears(1)));
        Assert.False(CourseworkLatePolicy.IsClosed(new CourseworkPart { CloseAfterDays = 1 }, _due.AddYears(1)));
    }

    [Fact]
    public async Task Submit_AfterTheCloseDate_IsRefused_AndLateWorkBeforeItIsTaken()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.CloseAfterDays = 1);
        await h.EnrollAsync("ana", Course);
        await h.EnrollAsync("ben", Course);

        // Act
        h.Clock.UtcNow = _due.AddHours(23);
        var late = await h.SubmitAsync("ana", Work, "Late answer");
        h.Clock.UtcNow = _due.AddDays(1).AddMinutes(1);
        var closed = await h.SubmitAsync("ben", Work, "Too late", ("essay.pdf", 100));

        // Assert
        Assert.True(late.Succeeded, late.Error);
        Assert.True(late.Submission.Late);
        Assert.Equal(SubmissionErrors.Closed, closed.Error);
        Assert.Empty(h.Files.Stored);
    }

    [Fact]
    public async Task Submit_WorkAGraderReturned_IsTakenAfterTheCloseDate()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.CloseAfterDays = 1);
        await h.EnrollAsync("ana", Course);
        var first = await h.SubmitAsync("ana", Work, "First");
        h.Clock.UtcNow = _due.AddDays(3);
        await h.ReturnAsync(first.Submission.ItemId, "grader");

        // Act
        var again = await h.SubmitAsync("ana", Work, "Second");

        // Assert
        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(2, again.Submission.Attempt);
    }

    [Fact]
    public async Task Grade_OnTimeWork_KeepsTheRawScore_WithoutAPenalty()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        h.Clock.UtcNow = _due.AddHours(-1);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(80m, graded.Submission.RawScore);
        Assert.Equal(0m, graded.Submission.LatePenaltyPercent);
        Assert.Equal(0, graded.Submission.DaysLate);
        Assert.Equal(80m, graded.Submission.Score);
    }

    [Fact]
    public async Task Grade_LateWork_TakesThePenalty_AndTheCourseGradeUsesThePenalisedScore()
    {
        // Arrange: handed in 25 hours late, two days at 10% a day.
        await using var h = await CreateAsync();
        h.AddCoursework(Course, "essay2");
        var ana = await h.EnrollAsync("ana", Course);
        h.Clock.UtcNow = _due.AddHours(25);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        var stored = await h.FindAsync(submitted.Submission.ItemId);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);
        var enrollment = await h.FindEnrollmentAsync(ana.ItemId);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(80m, stored.RawScore);
        Assert.Equal(2, stored.DaysLate);
        Assert.Equal(20m, stored.LatePenaltyPercent);
        Assert.Equal(64m, stored.Score);
        Assert.Equal(64m, progress.Score);
        Assert.Equal(32m, enrollment.Grade);
    }

    [Fact]
    public async Task Grade_VeryLateWork_TakesNoMoreThanTheCap()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        h.Clock.UtcNow = _due.AddDays(9);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 50);

        // Assert
        Assert.Equal(9, graded.Submission.DaysLate);
        Assert.Equal(30m, graded.Submission.LatePenaltyPercent);
        Assert.Equal(35m, graded.Submission.Score);
    }

    [Fact]
    public async Task Regrade_AppliesTheSamePenalty_ToTheNewRawScore_EvenWhenTheDueDateChanged()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        h.Clock.UtcNow = _due.AddHours(25);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        h.Courses.Get(Work).Alter<CourseworkPart>(part => part.DueUtc = _due.AddDays(7));

        // Act
        var regraded = await h.GradeAsync(submitted.Submission.ItemId, "grader2", points: 90, reason: "Missed the appendix");
        var stored = await h.FindAsync(submitted.Submission.ItemId);
        var enrollment = await h.FindEnrollmentAsync(ana.ItemId);

        // Assert
        Assert.True(regraded.Succeeded, regraded.Error);
        Assert.Equal(90m, stored.RawScore);
        Assert.Equal(2, stored.DaysLate);
        Assert.Equal(20m, stored.LatePenaltyPercent);
        Assert.Equal(72m, stored.Score);
        var earlier = Assert.Single(stored.History);
        Assert.Equal(80m, earlier.RawScore);
        Assert.Equal(20m, earlier.LatePenaltyPercent);
        Assert.Equal(64m, earlier.Score);
        Assert.Equal(72m, enrollment.Grade);
    }

    [Fact]
    public async Task Grade_UsesTheDueDateTheWorkWasHandedInAgainst_NotOneMovedAfterwards()
    {
        // Arrange: one learner on time, one 25 hours late; then the due date moves before anything is graded.
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        await h.EnrollAsync("ben", Course);
        h.Clock.UtcNow = _due.AddHours(-1);
        var onTime = await h.SubmitAsync("ana", Work, "Answer");
        h.Clock.UtcNow = _due.AddHours(25);
        var late = await h.SubmitAsync("ben", Work, "Answer");

        // Act: moved earlier, the on-time work would look late; moved later, the late work would look on time.
        h.Courses.Get(Work).Alter<CourseworkPart>(part => part.DueUtc = _due.AddDays(-2));
        await h.GradeAsync(onTime.Submission.ItemId, "grader", points: 80);
        h.Courses.Get(Work).Alter<CourseworkPart>(part => part.DueUtc = _due.AddDays(7));
        await h.GradeAsync(late.Submission.ItemId, "grader", points: 80);
        var anaWork = await h.FindAsync(onTime.Submission.ItemId);
        var benWork = await h.FindAsync(late.Submission.ItemId);

        // Assert
        Assert.Multiple(
            () => Assert.False(anaWork.Late),
            () => Assert.Equal(0, anaWork.DaysLate),
            () => Assert.Equal(80m, anaWork.Score),
            () => Assert.True(benWork.Late),
            () => Assert.Equal(2, benWork.DaysLate),
            () => Assert.Equal(20m, benWork.LatePenaltyPercent),
            () => Assert.Equal(64m, benWork.Score),
            () => Assert.Equal(_due, anaWork.DueUtc),
            () => Assert.Equal(_due, benWork.DueUtc));
    }

    private static async Task<CourseworkTestHarness> CreateAsync(Action<CourseworkPart> configure = null)
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse(Course);
        h.Courses.AddItem(Course, "lesson1");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = CourseworkSubmissionType.TextAndFiles;
            part.AllowedExtensions = [".pdf"];
            part.AllowResubmission = false;
            part.MaxPoints = 100;
            part.DueUtc = _due;
            part.LatePenaltyPercentPerDay = 10;
            part.MaxLatePenaltyPercent = 30;
            configure?.Invoke(part);
        });

        return h;
    }
}
