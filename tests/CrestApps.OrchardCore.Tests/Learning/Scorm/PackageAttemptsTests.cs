using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The SCORM settings Moodle packages carry: a limit on attempts, after which a launch only reviews, and the grading
/// method that turns the scores of the attempts into the item's score. A package without them keeps one attempt that
/// resumes, as before.
/// </summary>
public sealed class PackageAttemptsTests
{
    [Theory]
    [InlineData(GradingMethod.Highest, 90)]
    [InlineData(GradingMethod.Average, 75)]
    [InlineData(GradingMethod.First, 60)]
    [InlineData(GradingMethod.Latest, 90)]
    public void AttemptScores_CombineTheScoresByTheGradingMethod(GradingMethod method, decimal expected)
    {
        Assert.Equal(expected, AttemptScores.Combine([60m, 90m], method));
        Assert.Null(AttemptScores.Combine([], method));
    }

    [Fact]
    public async Task AFinishedAttempt_StartsANewOne_UntilTheLimit_ThenALaunchOnlyReviews()
    {
        // Arrange
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.Passed, configureItem: part => part.PassMark = 95);
        h.Courses.Get(PackageTestHarness.Package).Alter<LearningPackagePart>(part =>
        {
            part.MaxAttempts = 2;
            part.GradingMethod = GradingMethod.Highest;
        });
        var enrollment = await h.EnrollAsync("ana");

        // Act
        var first = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.core.score.raw", "90"), ("cmi.core.lesson_status", "failed")]);
        var second = await h.LaunchAsync("ana");
        var secondState = await h.StateAsync(enrollment.ItemId);
        await h.ApplyAsync("ana", second, finish: true, changes: [("cmi.core.score.raw", "60"), ("cmi.core.lesson_status", "failed")]);
        var progress = await h.ProgressAsync(enrollment.ItemId);
        var third = await h.LaunchAsync("ana");
        var thirdState = await h.StateAsync(enrollment.ItemId);

        // Assert
        Assert.False(first.Review);
        Assert.False(second.Review);
        Assert.Equal(2, secondState.Attempts);
        Assert.True(string.IsNullOrEmpty(secondState.Cmi.ScoreRaw));
        Assert.Equal("ab-initio", secondState.Cmi.Entry);
        Assert.Equal(90, progress.Score);
        Assert.True(third.Succeeded, third.Error);
        Assert.True(third.Review);
        Assert.True(third.AttemptsUsedUp);
        Assert.Equal(2, thirdState.Attempts);
        Assert.Equal([90m, 60m], thirdState.AttemptScores.Select(attempt => attempt.Score));
    }

    [Fact]
    public async Task TheAverageMethod_GivesTheItemTheMeanOfTheAttempts()
    {
        // Arrange
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.Passed, configureItem: part => part.PassMark = 95);
        h.Courses.Get(PackageTestHarness.Package).Alter<LearningPackagePart>(part => part.GradingMethod = GradingMethod.Average);
        var enrollment = await h.EnrollAsync("ana");

        // Act
        var first = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.core.score.raw", "60"), ("cmi.core.lesson_status", "failed")]);
        var second = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", second, finish: true, changes: [("cmi.core.score.raw", "80"), ("cmi.core.lesson_status", "failed")]);
        var progress = await h.ProgressAsync(enrollment.ItemId);

        // Assert
        Assert.Equal(70, progress.Score);
    }

    [Fact]
    public void TheItemScore_ShownToTheLearner_ComesFromTheAttemptsByTheGradingMethod()
    {
        // Arrange: found in the browser, where the panel showed the last attempt's 70 while the item had 55.
        var state = new PackageRuntimeState { AttemptScores = [new PackageAttemptScore { Number = 1, Score = 40 }, new PackageAttemptScore { Number = 2, Score = 70 }] };

        // Act
        var average = CrestApps.OrchardCore.Learning.Packages.Core.Services.PackageAttempts.ItemScore(state, new LearningPackagePart { GradingMethod = GradingMethod.Average });
        var none = CrestApps.OrchardCore.Learning.Packages.Core.Services.PackageAttempts.ItemScore(state, new LearningPackagePart());

        // Assert
        Assert.Equal(55, average);
        Assert.Null(none);
    }

    [Fact]
    public async Task APackageWithoutAttemptSettings_ResumesTheSameAttempt()
    {
        // Arrange
        await using var h = await PackageTestHarness.CreateAsync(completionMode: CompletionModes.Passed, configureItem: part => part.PassMark = 95);
        var enrollment = await h.EnrollAsync("ana");

        // Act
        var first = await h.LaunchAsync("ana");
        await h.ApplyAsync("ana", first, finish: true, changes: [("cmi.core.score.raw", "60"), ("cmi.core.lesson_status", "failed")]);
        var second = await h.LaunchAsync("ana");
        var state = await h.StateAsync(enrollment.ItemId);

        // Assert
        Assert.False(second.Review);
        Assert.Equal("60", state.Cmi.ScoreRaw);
        Assert.Equal(0, state.Attempts);
    }
}
