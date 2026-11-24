using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Tests.Learning.Credentials;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Review round 11, H1: a hidden (unpublished) course is not enrolled, but it never completes a learning path by being
/// left out. A set or a path left without an available course waits, an at-least minimum is not lowered, and what is
/// issued on a completed path is decided on the path with all its courses.
/// </summary>
public sealed class LearningPathHiddenCoursesRound11Tests
{
    [Fact]
    public async Task Round11H1_ACourseHiddenInTheNextStage_DoesNotCompleteThePath_AndNothingIsIssued()
    {
        // Arrange: c1 then c2, and c2 is unpublished to be revised.
        var queue = new RecordingCredentialAwardQueue();
        var h = await CreateAsync(queue);

        h.AddPath("p1",
            new LearningPathCourseSet { Id = "s1", CourseIds = ["c1"] },
            new LearningPathCourseSet { Id = "s2", Connector = LearningPathConnector.Then, CourseIds = ["c2"] });
        h.Inner.Courses.Get("c2").Published = false;

        // Act
        await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "p1", UserId = "ana", Actor = "admin" }));
        await h.CompleteCourseAsync("ana", "c1");

        var progress = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("p1", "ana"));
        var hidden = await h.EnrollmentsAsync("ana", "c2");

        // Assert
        Assert.Equal(LearningPathState.InProgress, progress.State);
        Assert.Null(progress.CompletedUtc);
        Assert.Equal(1, progress.OpenedStage);
        Assert.Empty(hidden);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Round11H1_APathWhoseCoursesAreAllHidden_DoesNotCompleteOnStart()
    {
        // Arrange
        var queue = new RecordingCredentialAwardQueue();
        var h = await CreateAsync(queue);

        h.AddPath("p1", new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "c2"] });
        h.Inner.Courses.Get("c1").Published = false;
        h.Inner.Courses.Get("c2").Published = false;

        // Act
        var started = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "p1", UserId = "ana", Actor = "admin" }));

        // Assert
        Assert.True(started.Created, started.Error);
        Assert.Equal(LearningPathState.InProgress, started.Progress.State);
        Assert.Null(started.Progress.CompletedUtc);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Round11H1_APathCompletedWithoutItsHiddenCourse_IssuesNothing()
    {
        // Arrange: one set of c1 and c2 where c2 is hidden; the path completes on c1, as a Moodle plan does.
        var queue = new RecordingCredentialAwardQueue();
        var h = await CreateAsync(queue);

        h.AddPath("p1", new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "c2"] });
        h.Inner.Courses.Get("c2").Published = false;

        // Act
        await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "p1", UserId = "ana", Actor = "admin" }));
        await h.CompleteCourseAsync("ana", "c1");

        var progress = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("p1", "ana"));
        var supports = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().SupportsIssuingAsync(progress));

        // Assert
        Assert.Equal(LearningPathState.Completed, progress.State);
        Assert.False(supports);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public void Round11H1_ASetEmptiedByHiding_IsNotComplete_AndAnAtLeastMinimumIsNotLowered()
    {
        // Arrange: (hidden1) or (c1); then at least 2 of c2, hidden2, hidden3.
        var path = new LearningPathPart
        {
            Sets =
            [
                new LearningPathCourseSet { Id = "s1", CourseIds = ["hidden1"] },
                new LearningPathCourseSet { Id = "s2", Connector = LearningPathConnector.Or, CourseIds = ["c1"] },
                new LearningPathCourseSet { Id = "s3", Connector = LearningPathConnector.Then, Join = LearningPathSetJoin.AtLeast, MinimumCount = 2, CourseIds = ["c2", "hidden2", "hidden3"] },
            ],
        };
        var available = new HashSet<string>(StringComparer.Ordinal) { "c1", "c2" };

        // Act
        var nothing = LearningPathRules.OpenStage(path, new HashSet<string>(StringComparer.Ordinal), available);
        var firstOr = LearningPathRules.OpenStage(path, new HashSet<string>(StringComparer.Ordinal) { "c1" }, available);
        var oneOfTwo = LearningPathRules.OpenStage(path, new HashSet<string>(StringComparer.Ordinal) { "c1", "c2" }, available);

        // Assert
        Assert.Equal(0, nothing);
        Assert.Equal(1, firstOr);
        Assert.Equal(1, oneOfTwo);
        Assert.False(LearningPathRules.IsSetComplete(path.Sets[0], new HashSet<string>(StringComparer.Ordinal), available));
    }

    private static Task<ProgramsHarness> CreateAsync(RecordingCredentialAwardQueue queue)
    {
        var definitions = new InMemoryCredentialDefinitions();

        definitions.Add("path-badge", new CredentialAwardRule { Kind = CredentialsConstants.RuleKinds.LearningPathCompleted, TargetId = "p1" });

        return ProgramsHarness.CreateAsync(services =>
        {
            services.AddSingleton<ICredentialDefinitionCatalog>(definitions);
            services.AddSingleton<ICredentialAwardQueue>(queue);
            services.AddScoped<CredentialRuleTrigger>();
            services.AddLearningPathObserver<CredentialLearningPathObserver>();
        });
    }
}
