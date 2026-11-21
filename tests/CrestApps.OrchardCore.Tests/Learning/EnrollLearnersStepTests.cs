using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Pipelines;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class EnrollLearnersStepTests
{
    [Fact]
    public async Task EnrollRowAsync_EnrollsWithAPipelineGrant_RejectsBadRows_AndIsIdempotentPerPipeline()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var known = new HashSet<string>(StringComparer.Ordinal) { "learner1" };
        var due = LearningTestHarness.Now.AddDays(30);

        Task<string> Enroll(string userId, string courseId)
            => harness.RunAsync(services => EnrollLearnersStep.EnrollRowAsync(services.GetRequiredService<IEnrollmentService>(), id => Task.FromResult(known.Contains(id)), "pipeline-1", userId, courseId, due, null, ignorePrerequisites: false));

        // Act
        var ok = await Enroll("learner1", "course1");
        var again = await Enroll("learner1", "course1");
        var unknownUser = await Enroll("ghost", "course1");
        var noCourse = await Enroll("learner1", "missing");
        var empty = await Enroll(" ", "course1");

        // Assert
        Assert.Null(ok);
        Assert.Null(again);
        Assert.NotNull(unknownUser);
        Assert.NotNull(noCourse);
        Assert.NotNull(empty);

        var enrollment = Assert.Single(await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByUserAsync("learner1"), commit: false));
        var grant = Assert.Single(enrollment.Grants);

        Assert.Equal(LearningConstants.EnrollmentSources.Pipeline, grant.Source);
        Assert.Equal("pipeline-1", grant.SourceId);
        Assert.Equal(due, enrollment.DueUtc);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }
}
