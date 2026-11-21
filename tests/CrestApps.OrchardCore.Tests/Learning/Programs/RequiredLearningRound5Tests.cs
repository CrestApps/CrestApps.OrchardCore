using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Review round 5, findings 3, 8 and 9: a refused or interrupted start is tried again by the next run and the person is
/// told only once it worked; duplicate assignments do not break a rule; a completion from before counts without a new
/// enrollment or a notice; and a person is told once that the learning is overdue, whoever notices first.
/// </summary>
public sealed class RequiredLearningRound5Tests
{
    [Fact]
    public async Task ARefusedStart_IsTriedAgainByTheNextRun_AndThePersonIsToldOnlyThen()
    {
        // Arrange: the course needs another one first, so enrolling is refused until ana completes it.
        var h = await ProgramsHarness.CreateAsync();
        h.Inner.Courses.AddCourse("late", part => part.Prerequisites = [new CrestApps.OrchardCore.Learning.Models.CoursePrerequisite { Kind = "Course", Id = "c1" }]);
        h.Inner.Courses.AddItem("late", "late-lesson");
        h.Audiences["field"] = ["ana"];
        var requirement = await SaveAsync(h, "late");

        // Act
        await RunAsync(h, requirement);
        var afterRefusal = await AssignmentAsync(h, requirement, "ana");
        var toldAfterRefusal = h.Observer.Events.Contains("required:ana");

        await h.CompleteCourseAsync("ana", "c1");
        await RunAsync(h, requirement);
        var afterStart = await AssignmentAsync(h, requirement, "ana");

        // Assert
        Assert.NotNull(afterRefusal.StartError);
        Assert.False(toldAfterRefusal);
        Assert.Null(afterStart.StartError);
        Assert.NotNull(afterStart.StartedUtc);
        Assert.Single(await h.EnrollmentsAsync("ana", "late"));
        Assert.Single(h.Observer.Events, value => value == "required:ana");
    }

    [Fact]
    public async Task AnAssignmentLeftWithoutEnrollment_AndDuplicates_AreHandledByTheNextRun()
    {
        // Arrange: a run that stopped after saving the assignments, and an overlapping run that saved a second one.
        var h = await ProgramsHarness.CreateAsync();
        h.Audiences["field"] = ["ana"];
        var requirement = await SaveAsync(h, "c1");

        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<RequiredLearningAssignmentStore>();

            await store.CreateAsync(new RequiredLearningAssignment { RequirementId = requirement.ItemId, UserId = "ana", State = RequiredLearningState.Required, RequiredUtc = h.Clock.UtcNow });
            await store.CreateAsync(new RequiredLearningAssignment { RequirementId = requirement.ItemId, UserId = "ana", State = RequiredLearningState.Required, RequiredUtc = h.Clock.UtcNow.AddSeconds(1) });
        });

        // Act
        var run = await RunAsync(h, requirement);
        var assignments = await h.RunAsync(async services => (await services.GetRequiredService<IRequiredLearningService>().PageAsync(requirement.ItemId, null, 0, 10)).Items);

        // Assert
        Assert.Equal(0, run.Added);
        Assert.Single(await h.EnrollmentsAsync("ana", "c1"));
        Assert.Single(assignments, assignment => assignment.IsOpen);
        Assert.Single(assignments, assignment => assignment.State == RequiredLearningState.NotRequired);
        Assert.Single(h.Observer.Events, value => value == "required:ana");
    }

    [Fact]
    public async Task ACompletionFromBefore_Counts_WithoutANewEnrollmentOrANotice()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();
        await h.CompleteCourseAsync("ana", "c2");
        h.Audiences["field"] = ["ana"];
        var requirement = await SaveAsync(h, "c2");

        // Act
        await RunAsync(h, requirement);
        var assignment = await AssignmentAsync(h, requirement, "ana");

        // Assert
        Assert.Equal(RequiredLearningState.Completed, assignment.State);
        Assert.Single(await h.EnrollmentsAsync("ana", "c2"));
        Assert.DoesNotContain("required:ana", h.Observer.Events);
    }

    [Fact]
    public async Task OverdueIsAnnouncedOnce_WhenTheRefreshSeesItFirst()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();
        h.Audiences["field"] = ["ana"];
        var requirement = await SaveAsync(h, "c3", dueDays: 1);

        await RunAsync(h, requirement);
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(3);

        // Act: the upkeep refreshes the rule before the reminder pass.
        await RunAsync(h, requirement);
        await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SendRemindersAsync());
        await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SendRemindersAsync());

        // Assert
        Assert.Single(h.Observer.Events, value => value == "overdue:ana");
        Assert.Equal(RequiredLearningState.Overdue, (await AssignmentAsync(h, requirement, "ana")).State);
    }

    private static async Task<LearningRequirement> SaveAsync(ProgramsHarness h, string courseId, int dueDays = 10)
    {
        var requirement = new LearningRequirement
        {
            Name = "Rule for " + courseId,
            TargetKind = ProgramsConstants.TargetKinds.Course,
            TargetId = courseId,
            AudienceName = "field",
            DueRule = RequiredLearningDueRule.DaysAfterRequired,
            DueDays = dueDays,
        };

        Assert.Empty(await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SaveAsync(requirement)));

        return requirement;
    }

    private static Task<RequiredLearningRunResult> RunAsync(ProgramsHarness h, LearningRequirement requirement)
        => h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().RunAsync(requirement.ItemId, "admin"));

    private static Task<RequiredLearningAssignment> AssignmentAsync(ProgramsHarness h, LearningRequirement requirement, string userId)
        => h.RunAsync(async services => (await services.GetRequiredService<IRequiredLearningService>().PageAsync(requirement.ItemId, null, 0, 10)).Items.Single(item => item.UserId == userId && item.State != RequiredLearningState.NotRequired));
}
