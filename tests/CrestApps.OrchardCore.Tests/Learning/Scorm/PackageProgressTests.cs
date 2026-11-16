using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// What a package reports reaches the learner's progress as a learner's own work: on a commit or a finish, through
/// Learning's learner path (access window and release rules apply), marked self-reported.
/// </summary>
public sealed class PackageProgressTests
{
    [Fact]
    public async Task CommitOfCompleted_CompletesTheItem_MarkedSelfReported()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_status", "completed")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Progress.ItemCompleted);
        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.True(item.SelfReported);
        Assert.Equal(1, item.Attempts);
    }

    [Fact]
    public async Task FinishOfPassed_CompletesTheItem_WithTheScore()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.score.raw", "90"), ("cmi.core.lesson_status", "passed")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.Equal(90m, item.Score);
        Assert.True(item.Passed);
        Assert.True(item.SelfReported);
    }

    [Fact]
    public async Task Failed_FailsAnItemThatCompletesWhenPassed()
    {
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.Passed);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.score.raw", "40"), ("cmi.core.lesson_status", "failed")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.Equal(CourseItemState.Failed, item.State);
        Assert.False(item.Passed);
        Assert.Equal(40m, item.Score);
    }

    [Fact]
    public async Task ScoreWithoutStatus_IsPassedByTheMasteryScore_AndCompletesAnItemThatCompletesWhenPassed()
    {
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.Passed, masteryScore: 80);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.score.raw", "85")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.Equal("passed", result.State.Cmi.LessonStatus);
        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.True(item.Passed);
    }

    [Fact]
    public async Task ScoreBelowTheMasteryScore_WithoutStatus_IsFailed_AndDoesNotCompleteTheItem()
    {
        await using var h = await PackageTestHarness.CreateAsync(masteryScore: 80);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.score.raw", "60")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.Equal("failed", result.State.Cmi.LessonStatus);
        Assert.NotEqual(CourseItemState.Completed, item.State);
        Assert.False(item.Passed);
    }

    [Fact]
    public async Task Incomplete_KeepsTheItemInProgress()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_status", "incomplete")]);
        var item = await h.ProgressAsync(enrollment.ItemId);

        Assert.NotEqual(CourseItemState.Completed, item.State);
        Assert.True(item.SelfReported);
    }

    [Fact]
    public async Task ValuesWithoutACommit_AreKept_ButNotReported()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, changes: [("cmi.core.lesson_status", "completed")]);

        Assert.Null(result.Progress);
        Assert.Equal("completed", (await h.StateAsync(enrollment.ItemId)).Cmi.LessonStatus);
        Assert.NotEqual(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId))?.State ?? CourseItemState.NotStarted);
    }

    [Fact]
    public async Task ReportedComplete_DoesNotCompleteAnItemTheLearnerMarksDone()
    {
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.MarkedDone);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.lesson_status", "completed")]);

        Assert.NotEqual(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId)).State);
    }

    [Fact]
    public async Task ScoreRange_IsScaledToAPercentage()
    {
        await using var h = await PackageTestHarness.CreateAsync(masteryScore: null);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.score.raw", "8"), ("cmi.core.score.max", "10"), ("cmi.core.lesson_status", "completed")]);

        Assert.Equal(80m, (await h.ProgressAsync(enrollment.ItemId)).Score);
    }

    [Fact]
    public async Task ExpiredAccessWindow_IsRefused_AndRecordsNothing()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.AccessEndsUtc = h.Clock.UtcNow.AddMinutes(-1));

        var result = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.lesson_status", "completed")]);

        Assert.Equal(PackageRuntimeErrors.AccessEnded, result.Error);
        Assert.Null(await h.ProgressAsync(enrollment.ItemId));
    }

    [Fact]
    public async Task ItemLockedAgainByAReleaseRule_IsRefusedByLearning()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        h.Courses.Get(PackageTestHarness.Package).Alter<CourseItemPart>(part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "intro" } },
        ]);

        var result = await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_status", "completed")]);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Progress.RefusedReason);
        Assert.NotEqual(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId))?.State ?? CourseItemState.NotStarted);
    }

    [Fact]
    public async Task Review_RecordsNothing()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.core.lesson_status", "passed")]);

        Assert.True(result.Review);
        Assert.Null(result.Progress);
        Assert.Null(await h.ProgressAsync(enrollment.ItemId));
    }
}
