using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// A learning path enrolls a person in the courses of its first stage, opens the next stage when they complete it,
/// completes when the last stage is done, and starting someone again changes nothing.
/// </summary>
public sealed class LearningPathServiceTests
{
    [Fact]
    public async Task StartAsync_EnrollsTheFirstStage_ThenEachCompletedStageOpensTheNext_UntilThePathCompletes()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();

        h.AddPath("onboarding",
            new LearningPathCourseSet { Id = "s1", CourseIds = ["c1"] },
            new LearningPathCourseSet { Id = "s2", Connector = LearningPathConnector.Then, Join = LearningPathSetJoin.Any, CourseIds = ["c2", "c3"] });

        // Act
        var started = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "onboarding", UserId = "ana", Actor = "admin" }));
        var c2BeforeStage = await h.EnrollmentsAsync("ana", "c2");

        await h.CompleteCourseAsync("ana", "c1");

        var afterFirst = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("onboarding", "ana"));
        var c2Opened = await h.EnrollmentsAsync("ana", "c2");
        var c3Opened = await h.EnrollmentsAsync("ana", "c3");

        await h.CompleteCourseAsync("ana", "c3");

        var done = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("onboarding", "ana"));
        var again = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "onboarding", UserId = "ana" }));
        var all = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().ListForUserAsync("ana"));

        // Assert
        Assert.True(started.Created, started.Error);
        Assert.Single(await h.EnrollmentsAsync("ana", "c1"));
        Assert.Equal(ProgramsConstants.PathSource, Assert.Single((await h.EnrollmentsAsync("ana", "c1"))[0].Grants).Source);
        Assert.Empty(c2BeforeStage);
        Assert.Equal(1, afterFirst.OpenedStage);
        Assert.Equal(LearningPathState.InProgress, afterFirst.State);
        Assert.Single(c2Opened);
        Assert.Single(c3Opened);
        Assert.Equal(LearningPathState.Completed, done.State);
        Assert.NotNull(done.CompletedUtc);
        Assert.Equal(["c1", "c3"], done.CompletedCourseIds);
        Assert.False(again.Created);
        Assert.Single(all);
    }

    [Fact]
    public async Task StartAsync_RefusesAPathWithoutCourses_AndAnUnknownPath()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();

        h.AddPath("empty");

        // Act
        var empty = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "empty", UserId = "ana" }));
        var unknown = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "nope", UserId = "ana" }));

        // Assert
        Assert.Null(empty.Progress);
        Assert.Null(unknown.Progress);
        Assert.False(string.IsNullOrEmpty(empty.Error));
    }
}
