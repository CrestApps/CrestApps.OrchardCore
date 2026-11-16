using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// SCORM 2004 packages in the runtime service: the launch values, every value replayed through the SCORM 2004 data model,
/// the total time of an attempt, and resume against a new attempt.
/// </summary>
public sealed class PackageRuntimeServiceScorm2004Tests
{
    private const string Item = PackageTestHarness.Package2004;

    [Fact]
    public async Task Launch_StartsAnAbInitioAttempt_WithTheLearnersReadOnlyValues()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");

        var launch = await h.LaunchAsync("ana", Item);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.True(launch.Succeeded, launch.Error);
        Assert.Equal(PackagesConstants.Standards.Scorm2004FourthEdition, state.Standard);
        Assert.Equal(1, state.Sessions);
        Assert.Equal(1, state.Attempts);

        var cmi = state.Cmi2004;

        Assert.Equal(4, cmi.Edition);
        Assert.Equal("ana", cmi.LearnerId);
        Assert.Equal("ana name", cmi.LearnerName);
        Assert.Equal("credit", cmi.Credit);
        Assert.Equal("normal", cmi.Mode);
        Assert.Equal("ab-initio", cmi.Entry);
        Assert.Equal("chapter=1", cmi.LaunchData);
        Assert.Equal("0.8", cmi.CompletionThreshold);
        Assert.Equal("0.6", cmi.ScaledPassingScore);
        Assert.Equal("PT30M", cmi.MaxTimeAllowed);
        Assert.Equal("exit,message", cmi.TimeLimitAction);
        Assert.Equal(["PRIMARY", "obj-2"], cmi.Objectives.Select(objective => objective.Id));
    }

    [Fact]
    public async Task Launch_OfAThirdEditionPackage_UsesTheThirdEditionRules()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync(standard: PackagesConstants.Standards.Scorm2004ThirdEdition, completionThreshold: null, scaledPassingScore: null);
        var enrollment = await h.EnrollAsync("ana");

        await h.LaunchAsync("ana", Item);
        var cmi = (await h.StateAsync(enrollment.ItemId, Item)).Cmi2004;

        Assert.Equal(3, cmi.Edition);
        Assert.Null(cmi.CompletionThreshold);
        Assert.Null(cmi.ScaledPassingScore);
    }

    [Fact]
    public async Task Apply_ReplaysEveryValueThroughThe2004DataModel_AndKeepsOnlyTheValidOnes()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        var result = await h.ApplyAsync("ana", launch, commit: true, changes:
        [
            ("cmi.location", "slide-3"),
            ("cmi.learner_id", "x"),
            ("cmi.completion_status", "passed"),
            ("cmi.score.scaled", "2"),
            ("cmi.objectives.0.success_status", "passed"),
            ("cmi.interactions.0.type", "choice"),
            ("cmi.interactions.0.id", "q1"),
            ("cmi.interactions.0.type", "choice"),
            ("cmi.interactions.0.learner_response", "a[,]b"),
        ]);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            [(1, Scorm2004Errors.ReadOnly), (2, Scorm2004Errors.TypeMismatch), (3, Scorm2004Errors.ValueOutOfRange), (5, Scorm2004Errors.DependencyNotEstablished)],
            result.Refused.Select(refusal => (refusal.Index, refusal.Code)));
        Assert.Equal("slide-3", state.Cmi2004.Location);
        Assert.Equal("ana", state.Cmi2004.LearnerId);
        Assert.Null(state.Cmi2004.ScoreScaled);
        Assert.Equal("passed", state.Cmi2004.Objectives[0].SuccessStatus);
        Assert.Equal("a[,]b", Assert.Single(state.Cmi2004.Interactions).LearnerResponse);
    }

    [Fact]
    public async Task Finish_AddsTheSessionTimeToTheTotal_AndEndsTheLaunch()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana", Item);

        var finished = await h.ApplyAsync("ana", launch, finish: true, changes: [("cmi.session_time", "PT2M30S"), ("adl.nav.request", "exitAll")]);
        var again = await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.location", "late")]);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.True(finished.Succeeded, finished.Error);
        Assert.Equal("PT0H2M30S", state.Cmi2004.TotalTime);
        Assert.Null(state.Cmi2004.SessionTime);
        Assert.Equal("exitAll", state.Cmi2004.NavRequest);
        Assert.Null(state.CurrentLaunchId);
        Assert.Equal(h.Clock.UtcNow, state.LastFinishedUtc);
        Assert.Equal(PackageRuntimeErrors.Superseded, again.Error);
    }

    [Fact]
    public async Task LaunchAfterASuspendedSession_ResumesTheAttempt_AndTheTotalTimeAccumulates()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.location", "slide-7"), ("cmi.suspend_data", "page=7"), ("cmi.exit", "suspend"), ("cmi.session_time", "PT10M")]);

        var second = await h.LaunchAsync("ana", Item);
        var resumed = await h.StateAsync(enrollment.ItemId, Item);
        await h.ApplyAsync("ana", second, finish: true, changes: [("cmi.session_time", "PT5M20S")]);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal("resume", resumed.Cmi2004.Entry);
        Assert.Equal("slide-7", resumed.Cmi2004.Location);
        Assert.Equal("page=7", resumed.Cmi2004.SuspendData);
        Assert.Equal(string.Empty, resumed.Cmi2004.Exit);
        Assert.Equal(2, resumed.Sessions);
        Assert.Equal(1, resumed.Attempts);
        Assert.Equal("PT0H15M20S", state.Cmi2004.TotalTime);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("")]
    public async Task LaunchAfterAnAttemptEnded_StartsANewAttempt_WithItsDataReset(string exit)
    {
        await using var h = await PackageTestHarness.CreateAsync();
        await h.AddScorm2004PackageAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana", Item);
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.location", "slide-9"), ("cmi.exit", exit), ("cmi.session_time", "PT10M"), ("cmi.objectives.0.success_status", "failed"), ("cmi.objectives.2.id", "extra")]);

        await h.LaunchAsync("ana", Item);
        var state = await h.StateAsync(enrollment.ItemId, Item);

        Assert.Equal("ab-initio", state.Cmi2004.Entry);
        Assert.Null(state.Cmi2004.Location);
        Assert.Equal("PT0H0M0S", state.Cmi2004.TotalTime);
        Assert.Equal(["PRIMARY", "obj-2"], state.Cmi2004.Objectives.Select(objective => objective.Id));
        Assert.Equal("unknown", state.Cmi2004.Objectives[0].SuccessStatus);
        Assert.Equal(2, state.Sessions);
        Assert.Equal(2, state.Attempts);
    }

    [Fact]
    public async Task Launch_AfterAScorm12PackageWasReplacedByA2004One_StartsFreshOn2004()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var first = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.core.lesson_location", "p3"), ("cmi.core.exit", "suspend")]);

        h.Courses.Get(PackageTestHarness.Package).Alter<LearningPackagePart>(part =>
        {
            part.Standard = PackagesConstants.Standards.Scorm2004ThirdEdition;
            part.ObjectiveIds = ["PRIMARY"];
        });

        var second = await h.LaunchAsync("ana");
        var state = await h.StateAsync(enrollment.ItemId);

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(PackagesConstants.Standards.Scorm2004ThirdEdition, state.Standard);
        Assert.Equal("ab-initio", state.Cmi2004.Entry);
        Assert.Equal(3, state.Cmi2004.Edition);
        Assert.Equal(["PRIMARY"], state.Cmi2004.Objectives.Select(objective => objective.Id));
        Assert.Equal(1, state.Attempts);
    }

    [Fact]
    public async Task Apply_ToAScorm12Package_StillUsesTheScorm12Model()
    {
        await using var h = await PackageTestHarness.CreateAsync();
        var enrollment = await h.EnrollAsync("ana");
        var launch = await h.LaunchAsync("ana");

        var result = await h.ApplyAsync("ana", launch, commit: true, changes: [("cmi.core.lesson_location", "p3"), ("cmi.location", "x")]);
        var state = await h.StateAsync(enrollment.ItemId);

        Assert.Equal([(1, Scorm12Errors.InvalidArgument)], result.Refused.Select(refusal => (refusal.Index, refusal.Code)));
        Assert.Equal(PackagesConstants.Standards.Scorm12, state.Standard);
        Assert.Null(state.Cmi2004);
        Assert.Equal("p3", state.Cmi.LessonLocation);
    }
}
