using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.SharedSkills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Dispatch;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// A skill Contact Center gives an agent is stored on the agent's user. A worker linked to that user holds it too, so
/// Workforce's scheduler and coverage, and Dispatch's matching, see the skills agents were given in Contact Center.
/// </summary>
public sealed class ContactCenterAgentSkillsTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task ASkillGivenToAContactCenterAgent_IsHeldByTheirWorkerProfile()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var spanish = await PlanningKit.AddSkillAsync(harness, "Spanish");

        // Act
        await GiveAgentSkillAsync(harness, ana.UserId, "Spanish", proficiency: 4);

        var (holds, effective) = await harness.RunAsync(async services =>
        {
            var assignments = services.GetRequiredService<ISkillAssignmentService>();

            return (
                await assignments.HoldsAsync(SkillSubject.Worker(ana.ItemId), spanish.ItemId, 4, _monday),
                await assignments.GetEffectiveAsync(SkillSubject.Worker(ana.ItemId), _monday));
        }, commit: false);

        // Assert
        Assert.True(holds);
        Assert.Equal([spanish.ItemId], effective.Select(assignment => assignment.SkillId));
    }

    [Fact]
    public async Task Coverage_ForASkill_CountsAWorkerWhoseAgentHoldsIt()
    {
        // Arrange (Ana and Ben work Monday; only Ana's agent speaks Spanish)
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var spanish = await PlanningKit.AddSkillAsync(harness, "Spanish");
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await GiveAgentSkillAsync(harness, ana.UserId, "Spanish");
        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        // Act
        var intervals = await harness.RunAsync(services => services.GetRequiredService<ICoverageService>().ComputeAsync(new CoverageQuery
        {
            TeamId = team.ItemId,
            From = _monday,
            To = _monday,
            ScenarioId = period.PrimaryScenarioId,
            SkillId = spanish.ItemId,
        }), commit: false);

        // Assert
        Assert.Equal(1, intervals.Single(interval => interval.StartUtc == PlanningKit.Utc(_monday, new TimeOnly(10, 0))).Scheduled);
    }

    [Fact]
    public async Task AutoSchedule_PutsTheWorkerWhoseAgentHoldsTheSkill_OnEveryDayThatNeedsIt()
    {
        // Arrange (a Spanish speaker is needed every weekday; only Cy's agent speaks Spanish)
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var members = new List<WorkerProfile>();

        foreach (var name in new[] { "Ana", "Ben", "Cy" })
        {
            members.Add(await PlanningKit.AddMemberAsync(harness, name, team));
        }

        var spanish = await PlanningKit.AddSkillAsync(harness, "Spanish");

        await PlanningKit.AddTemplateAsync(harness, "Day", new TimeOnly(9, 0));
        await SaveRequirementAsync(harness, team, "Front desk", null, minimum: 1, target: 2);
        await SaveRequirementAsync(harness, team, "Spanish on duty", spanish.ItemId, minimum: 1, target: 1);
        await GiveAgentSkillAsync(harness, members[2].UserId, "Spanish");

        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().AutoScheduleAsync(period.PrimaryScenarioId, new AutoScheduleOptions { TimeLimitSeconds = 10 }, "tester"));
        var drafts = await PlanningKit.ListDraftsAsync(harness, period);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(5, drafts.Count(draft => draft.WorkerId == members[2].ItemId));
    }

    [Fact]
    public async Task DispatchCandidates_IncludeATechnicianWhoseAgentHoldsTheSkill()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var boilers = await PlanningKit.AddSkillAsync(kit.Harness, "Boilers");
        var ana = await kit.AddTechnicianAsync("Ana Field", worker => worker.UserId = "user-ana-field");
        var ben = await kit.AddTechnicianAsync("Ben Field", worker => worker.UserId = "user-ben-field");

        await kit.PublishShiftsAsync(ana, ben);
        await GiveAgentSkillAsync(kit.Harness, ana.UserId, "Boilers");

        var (_, visit) = await kit.AddJobAsync("Boiler down", DispatchKit.South, workOrder => workOrder.RequiredSkills = [new RequiredSkill { SkillId = boilers.ItemId }]);

        // Act
        var found = await kit.CandidatesAsync(visit);

        // Assert
        Assert.Equal(["Ana Field"], found.Candidates.Select(candidate => candidate.Name));
        Assert.Contains(found.Excluded, exclusion => exclusion.Name == "Ben Field");
    }

    // Writes the skill the way Contact Center does: by name, on the agent's user.
    private static Task GiveAgentSkillAsync(CalendarTestHarness harness, string userId, string skillName, int proficiency = 3)
    {
        return harness.RunAsync(services =>
        {
            var sync = new AgentSkillsSync(
                services.GetRequiredService<ISkillManager>(),
                services.GetRequiredService<ISkillAssignmentService>(),
                services.GetRequiredService<global::OrchardCore.Modules.IClock>());

            return sync.PushAsync(new AgentProfile
            {
                UserId = userId,
                SkillProficiencies = [new AgentSkill { SkillId = skillName, Proficiency = proficiency }],
            }, removeMissing: false);
        });
    }

    private static Task SaveRequirementAsync(CalendarTestHarness harness, Team team, string name, string skillId, int minimum, int target)
    {
        return harness.RunAsync(async services =>
        {
            var requirement = new CoverageRequirement
            {
                TeamId = team.ItemId,
                Name = name,
                SkillId = skillId,
                Rows = [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                    .Select(day => new CoverageRow { Day = day, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), Minimum = minimum, Target = target })],
            };

            var saved = await services.GetRequiredService<ICoverageService>().SaveRequirementAsync(requirement);

            Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        });
    }
}
