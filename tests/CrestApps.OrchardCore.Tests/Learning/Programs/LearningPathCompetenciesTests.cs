using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Competencies (Moodle migration item 14): a learning path, as a Moodle learning-plan template, grants its
/// competencies when a person completes it, only when the completed path supports issuing.
/// </summary>
public sealed class LearningPathCompetenciesTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CompletingAPath_GrantsItsCompetencies_OnlyWhenThePathSupportsIssuing(bool failFirstCourse, bool grants)
    {
        // Arrange
        var granted = new List<(SkillSubject Subject, SkillAssignmentInput Input, string Actor)>();
        var skills = new Mock<ISkillAssignmentService>();

        skills.Setup(service => service.GetAssignmentsAsync(It.IsAny<SkillSubject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        skills.Setup(service => service.AssignAsync(It.IsAny<SkillSubject>(), It.IsAny<SkillAssignmentInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((SkillSubject subject, SkillAssignmentInput input, string actor, CancellationToken _) => granted.Add((subject, input, actor)))
            .ReturnsAsync(new SkillsResult());

        var h = await ProgramsHarness.CreateAsync(services =>
        {
            services.AddSingleton(skills.Object);
            services.AddLearningPathObserver<LearningPathCompetenciesObserver>();
        });

        h.AddPath("plan", new LearningPathCourseSet { Id = "s1", CourseIds = ["c1", "c2"] });
        h.Path("plan").Apply(new CompetenciesPart { SkillIds = ["communication", "teamwork"], Proficiency = 2 });

        // Act
        await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().StartAsync(new LearningPathStartRequest { PathId = "plan", UserId = "ana", Actor = "admin" }));
        await h.CompleteCourseAsync("ana", "c1");

        if (failFirstCourse)
        {
            await h.RunAsync(async services =>
            {
                var store = services.GetRequiredService<EnrollmentStore>();
                var enrollment = (await store.ListByCourseForUserAsync("ana", "c1")).Single();

                enrollment.Passed = false;
                await store.SaveAsync(enrollment);
            });
        }

        await h.CompleteCourseAsync("ana", "c2");

        var progress = await h.RunAsync(services => services.GetRequiredService<ILearningPathService>().FindProgressAsync("plan", "ana"));

        // Assert
        Assert.Equal(LearningPathState.Completed, progress.State);

        if (grants)
        {
            Assert.Equal(["communication", "teamwork"], granted.Select(item => item.Input.SkillId).Order());
            Assert.All(granted, item =>
            {
                Assert.Equal(SkillSubject.User("ana"), item.Subject);
                Assert.Equal(2, item.Input.Proficiency);
                Assert.StartsWith("Learning", item.Actor, StringComparison.Ordinal);
            });
        }
        else
        {
            Assert.Empty(granted);
        }
    }
}
