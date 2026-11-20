using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Competencies (Moodle migration item 14): a course grants Skills skills at a level when a learner completes it, only
/// when the completion supports issuing (the rule credentials and credits follow), never lowering a level the learner
/// already holds.
/// </summary>
public sealed class CompetencyGrantTests
{
    [Fact]
    public async Task CompletingACourse_GrantsItsCompetencies_AtTheLevel_FromTheCompletionDay()
    {
        // Arrange
        await using var h = await CreateAsync();
        var lifting = await AddSkillAsync(h, "Safe lifting", "Workplace safety");
        var ladders = await AddSkillAsync(h, "Ladder use", "Workplace safety");

        h.Inner.Courses.Get("safety").Alter<CompetenciesPart>(part =>
        {
            part.SkillIds = [lifting.ItemId, ladders.ItemId];
            part.Proficiency = 4;
        });

        // Act
        await h.CompleteCourseAsync("ana", "safety");

        // Assert
        var held = await AssignmentsAsync(h, "ana");

        Assert.Equal(2, held.Count);
        Assert.All(held, assignment =>
        {
            Assert.Equal(4, assignment.Proficiency);
            Assert.Equal(CertificationHarness.Today, assignment.EffectiveFrom);
            Assert.Null(assignment.EffectiveTo);
            Assert.StartsWith("Learning", assignment.AssignedBy, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ACompletionRestingOnABrowserResult_GrantsNothing_UnlessTheItemAllowsIt()
    {
        // Arrange
        await using var h = await CreateAsync();
        var skill = await AddSkillAsync(h, "Safe lifting", "Workplace safety");

        h.Inner.Courses.AddCourse("strict");
        h.Inner.Courses.AddItem("strict", "package-strict", "Package", CompletionModes.PackageReported);
        h.Inner.Courses.AddCourse("lenient");
        h.Inner.Courses.AddItem("lenient", "package-lenient", "Package", CompletionModes.PackageReported, configure: part => part.AllowSelfReported = true);

        foreach (var course in new[] { "strict", "lenient" })
        {
            h.Inner.Courses.Get(course).Alter<CompetenciesPart>(part => part.SkillIds = [skill.ItemId]);
        }

        // Act
        await ReportFromTheBrowserAsync(h, "ana", "strict", "package-strict");
        await ReportFromTheBrowserAsync(h, "ben", "lenient", "package-lenient");

        // Assert
        Assert.Empty(await AssignmentsAsync(h, "ana"));
        Assert.Equal(CompetenciesPart.DefaultProficiency, Assert.Single(await AssignmentsAsync(h, "ben")).Proficiency);
    }

    [Fact]
    public async Task AFailedCompletion_GrantsNothing()
    {
        // Arrange
        await using var h = await CreateAsync();
        var skill = await AddSkillAsync(h, "Safe lifting", "Workplace safety");

        h.Inner.Courses.Get("safety").Alter<CompetenciesPart>(part => part.SkillIds = [skill.ItemId]);

        // Act
        await h.RunAsync(services => new CompetencyEnrollmentObserver(services, Microsoft.Extensions.Logging.Abstractions.NullLogger<CompetencyEnrollmentObserver>.Instance).CompletedAsync(new Enrollment
        {
            ItemId = "e1",
            UserId = "ana",
            CourseId = "safety",
            Status = EnrollmentStatus.Completed,
            Passed = false,
            CompletedUtc = LearningTestHarness.Now,
        }));

        // Assert
        Assert.Empty(await AssignmentsAsync(h, "ana"));
    }

    [Fact]
    public async Task Granting_NeverLowersALevel_RaisesALowerOne_AndRestartsAnEndedOne()
    {
        // Arrange: ana holds lifting at 5, ladders at 2 and first aid until last month.
        await using var h = await CreateAsync();
        var lifting = await AddSkillAsync(h, "Safe lifting", "Workplace safety");
        var ladders = await AddSkillAsync(h, "Ladder use", "Workplace safety");
        var firstAid = await AddSkillAsync(h, "First aid", "Workplace safety");
        var lastMonth = CertificationHarness.Today.AddMonths(-1);

        await h.RunAsync(async services =>
        {
            var skills = services.GetRequiredService<ISkillAssignmentService>();

            await skills.AssignAsync(SkillSubject.User("ana"), new SkillAssignmentInput { SkillId = lifting.ItemId, Proficiency = 5, EffectiveFrom = lastMonth.AddYears(-1) }, "admin");
            await skills.AssignAsync(SkillSubject.User("ana"), new SkillAssignmentInput { SkillId = ladders.ItemId, Proficiency = 2, EffectiveFrom = lastMonth.AddYears(-1) }, "admin");
            await skills.AssignAsync(SkillSubject.User("ana"), new SkillAssignmentInput { SkillId = firstAid.ItemId, Proficiency = 4, EffectiveFrom = lastMonth.AddYears(-2), EffectiveTo = lastMonth }, "admin");
        });

        var part = new CompetenciesPart { SkillIds = [lifting.ItemId, ladders.ItemId, firstAid.ItemId], Proficiency = 3 };

        // Act
        var granted = await h.RunAsync(services => CompetencyGrants.GrantAsync(services.GetRequiredService<ISkillAssignmentService>(), part, "ana", CertificationHarness.Today, "Learning: Safety"));

        // Assert
        var held = (await AssignmentsAsync(h, "ana")).ToDictionary(assignment => assignment.SkillId);

        Assert.Equal(2, granted);
        Assert.Equal(5, held[lifting.ItemId].Proficiency);
        Assert.Equal("admin", held[lifting.ItemId].AssignedBy);
        Assert.Equal(3, held[ladders.ItemId].Proficiency);
        Assert.Equal(lastMonth.AddYears(-1), held[ladders.ItemId].EffectiveFrom);
        Assert.Equal(4, held[firstAid.ItemId].Proficiency);
        Assert.Equal(CertificationHarness.Today, held[firstAid.ItemId].EffectiveFrom);
        Assert.Null(held[firstAid.ItemId].EffectiveTo);
    }

    [Fact]
    public async Task Granting_SkipsADisabledOrDeletedSkill_AndGrantsTheOthers()
    {
        // Arrange
        await using var h = await CreateAsync();
        var lifting = await AddSkillAsync(h, "Safe lifting", "Workplace safety");
        var retired = await AddSkillAsync(h, "Retired", "Workplace safety", skill => skill.Enabled = false);
        var part = new CompetenciesPart { SkillIds = [retired.ItemId, "gone", lifting.ItemId, lifting.ItemId], Proficiency = 9 };

        // Act
        var granted = await h.RunAsync(services => CompetencyGrants.GrantAsync(services.GetRequiredService<ISkillAssignmentService>(), part, "ana", CertificationHarness.Today, "Learning: Safety"));

        // Assert: the level is kept on the Skills scale.
        Assert.Equal(1, granted);
        Assert.Equal(SkillProficiency.Maximum, Assert.Single(await AssignmentsAsync(h, "ana")).Proficiency);
    }

    private static async Task<CertificationHarness> CreateAsync()
    {
        CertificationHarness h = null;
        h = await CertificationHarness.CreateAsync(configure: services =>
        {
            var content = new Mock<IContentManager>();

            content.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => h.Inner.Courses.Get(id));
            services.RemoveAll<IContentManager>();
            services.AddSingleton(content.Object);
            services.AddLearningCompetenciesCore();
        });

        return h;
    }

    private static Task<Skill> AddSkillAsync(CertificationHarness h, string name, string category, Action<Skill> configure = null)
        => h.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            skill.Category = category;
            configure?.Invoke(skill);
            await manager.CreateAsync(skill);

            return skill;
        });

    private static Task<IReadOnlyList<SkillAssignment>> AssignmentsAsync(CertificationHarness h, string userId)
        => h.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.User(userId)));

    private static async Task ReportFromTheBrowserAsync(CertificationHarness h, string userId, string courseId, string itemId)
    {
        var enrollment = await h.Inner.EnrollAsync(userId, courseId);

        await h.RunAsync(async services =>
        {
            var fresh = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId);
            var update = await services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(fresh, new CourseItemResult { ItemId = itemId, ReportedComplete = true, SelfReported = true });

            Assert.True(update.CourseCompleted);
        });
    }
}
