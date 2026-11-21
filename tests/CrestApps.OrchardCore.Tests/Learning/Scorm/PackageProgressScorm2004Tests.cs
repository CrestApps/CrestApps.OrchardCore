using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// What a SCORM 2004 package reports reaches the learner's progress as for SCORM 1.2: on a commit or a terminate, through
/// Learning's learner path, marked self-reported, from the evaluated completion and success statuses.
/// </summary>
public sealed class PackageProgressScorm2004Tests
{
    private const string Item = PackageTestHarness.Package2004;

    [Fact]
    public async Task CommitOfCompleted_CompletesTheItem_AsAResultReportedByTheBrowser()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionThreshold: null);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        var result = await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.completion_status", "completed")]);
        var item = await h.ProgressAsync(enrollment.ItemId, Item);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Progress.ItemCompleted);
        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.True(item.SelfReported);
        Assert.True(item.ResultSelfReported);
        Assert.Equal(1, item.Attempts);
    }

    [Fact]
    public async Task ProgressMeasureReachingTheThreshold_CompletesTheItem()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionThreshold: 0.8m);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.completion_status", "incomplete"), ("cmi.progress_measure", "0.85")]);

        Assert.Equal(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId, Item)).State);
    }

    [Fact]
    public async Task CompletedBelowTheThreshold_DoesNotCompleteTheItem()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionThreshold: 0.8m);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.completion_status", "completed"), ("cmi.progress_measure", "0.5")]);

        Assert.NotEqual(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId, Item)).State);
    }

    [Fact]
    public async Task ScaledScoreAtThePassingScore_PassesAnItemThatCompletesWhenPassed_WithTheScore()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionMode: CompletionModes.Passed, scaledPassingScore: 0.6m);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.score.scaled", "0.9"), ("cmi.score.raw", "18"), ("cmi.score.max", "20")]);
        var item = await h.ProgressAsync(enrollment.ItemId, Item);

        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.True(item.Passed);
        Assert.Equal(90m, item.Score);
        Assert.True(item.ResultSelfReported);
    }

    [Fact]
    public async Task Failed_FailsAnItemThatCompletesWhenPassed()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(completionMode: CompletionModes.Passed, scaledPassingScore: null);
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.success_status", "failed"), ("cmi.score.scaled", "0.4")]);
        var item = await h.ProgressAsync(enrollment.ItemId, Item);

        Assert.Equal(CourseItemState.Failed, item.State);
        Assert.False(item.Passed);
        Assert.Equal(40m, item.Score);
    }

    [Fact]
    public async Task NewAttempt_IsCountedInTheProgress()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.exit", "normal")]);
        var second = await h.LaunchAsync("ana", Item);

        await h.ApplyAsync("ana", second, commit: true, changes: [("cmi.location", "p1")]);

        Assert.Equal(2, (await h.ProgressAsync(enrollment.ItemId, Item)).Attempts);
    }

    [Fact]
    public async Task Review_OfACompletedEnrollment_IsNoCreditReview_AndRecordsAndKeepsNothing()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.location", "kept"), ("cmi.exit", "normal")]);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);
        var before = await h.ProgressAsync(enrollment.ItemId, Item);

        var review = await h.LaunchAsync("ana", Item);
        var launched = await h.StateAsync(enrollment.ItemId, Item);
        var result = await h.ApplyAsync("ana", review, finish: true, changes: [("cmi.location", "changed"), ("cmi.completion_status", "completed"), ("cmi.learner_id", "x")]);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.True(review.Review);
        Assert.Equal("review", launched.Cmi2004.Mode);
        Assert.Equal("no-credit", launched.Cmi2004.Credit);
        Assert.Equal(1, launched.Attempts);
        Assert.True(result.Review);
        Assert.Null(result.Progress);
        Assert.Equal(["cmi.learner_id"], result.Refused.Select(refusal => refusal.Element));
        Assert.Equal("kept", state.Cmi2004.Location);
        Assert.Equal(before.State, (await h.ProgressAsync(enrollment.ItemId, Item)).State);
    }

    [Theory]
    [InlineData("normal", "")]
    [InlineData("suspend", "resume")]
    public async Task Review_ReportsTheEntryOfTheAttemptItReopens(string exit, string entry)
    {
        // SCORM 2004 RTE cmi.entry: ab-initio only for the first access of a new learner attempt, resume after a suspended
        // session, and empty otherwise. A review starts no attempt, so it is never ab-initio.
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.location", "p3"), ("cmi.exit", exit)]);
        await h.ChangeEnrollmentAsync(enrollment.ItemId, e => e.Status = EnrollmentStatus.Completed);

        await h.LaunchAsync("ana", Item);
        var launched = await h.StateAsync(enrollment.ItemId, Item);

        Assert.Equal("review", launched.Cmi2004.Mode);
        Assert.Equal(entry, launched.Cmi2004.Entry);
    }
}
