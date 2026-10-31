using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Auto-scheduling a draft on a real SQLite store: the solver's problem is built from the team's members, templates,
/// coverage, availability and skills, the shifts it proposes land in the draft, the shifts already there stay, and the
/// run is recorded. Coverage for a skill counts only the workers who hold it.
/// </summary>
public sealed class AutoScheduleTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task AutoSchedule_FillsTheDraftToTheTarget_WithTheTeamsActiveMembers()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        // Act
        var result = await AutoScheduleAsync(harness, setup.Period);
        var drafts = await PlanningKit.ListDraftsAsync(harness, setup.Period);
        var scenario = await FindScenarioAsync(harness, setup.Period);

        // Assert: two people each weekday, nobody at the weekend
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal([2, 2, 2, 2, 2], Enumerable.Range(0, 5).Select(day => drafts.Count(draft => draft.Date == _monday.AddDays(day))));
        Assert.DoesNotContain(drafts, draft => draft.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.All(drafts, draft =>
        {
            Assert.Contains(draft.WorkerId, setup.Members.Select(worker => worker.ItemId));
            Assert.Equal(setup.Day.ItemId, draft.TemplateId);
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(draft.Start));
            Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromDateTime(draft.End));
            Assert.False(string.IsNullOrEmpty(draft.ShiftKey));
        });
        Assert.Equal(drafts.Count, drafts.Select(draft => (draft.WorkerId, draft.Date)).Distinct().Count());

        Assert.NotNull(scenario.LastRun);
        Assert.Equal(WorkforceConstants.DefaultSolver, scenario.LastRun.Solver);
        Assert.Equal(10, scenario.LastRun.ShiftsAdded);
        Assert.Equal(1, scenario.LastRun.CoverageScore, 6);
        Assert.Equal("tester", scenario.LastRun.RunBy);
        Assert.Equal(CalendarTestHarness.Now, scenario.LastRun.RunUtc);
    }

    [Fact]
    public async Task AutoSchedule_KeepsTheShiftsAlreadyInTheDraft_AndCountsThem()
    {
        // Arrange (Ana already works Monday)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var ana = setup.Members[0];
        var existing = await PlanningKit.AddDraftAsync(harness, setup.Period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0), setup.Day.ItemId);

        // Act
        await AutoScheduleAsync(harness, setup.Period);
        var monday = (await PlanningKit.ListDraftsAsync(harness, setup.Period)).Where(draft => draft.Date == _monday).ToArray();

        // Assert
        Assert.Equal(2, monday.Length);
        Assert.Single(monday, draft => draft.WorkerId == ana.ItemId);
        Assert.Contains(monday, draft => draft.ItemId == existing.ItemId && draft.ShiftKey == existing.ShiftKey);
    }

    [Fact]
    public async Task AutoSchedule_RespectsTheWorkersAvailability()
    {
        // Arrange (Ben is unavailable on Wednesdays)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, ben => ben.Availability.Add(new AvailabilityWindow { Day = DayOfWeek.Wednesday, Preference = AvailabilityPreference.Unavailable }));
        var ben = setup.Members[1];

        // Act
        await AutoScheduleAsync(harness, setup.Period);
        var drafts = await PlanningKit.ListDraftsAsync(harness, setup.Period);

        // Assert
        Assert.DoesNotContain(drafts, draft => draft.WorkerId == ben.ItemId && draft.Date == _monday.AddDays(2));
        Assert.Equal(2, drafts.Count(draft => draft.Date == _monday.AddDays(2)));
    }

    [Fact]
    public async Task AutoSchedule_PutsTheSkilledWorkerOnEveryDayThatNeedsTheSkill()
    {
        // Arrange (a lead is needed every weekday and only Cy holds the skill)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var lead = await PlanningKit.AddSkillAsync(harness, "Lead");
        var cy = setup.Members[2];

        await PlanningKit.GrantSkillAsync(harness, cy.ItemId, lead.ItemId);
        await SaveRequirementAsync(harness, setup.Team, "Lead on duty", lead.ItemId, minimum: 1, target: 1);

        // Act
        await AutoScheduleAsync(harness, setup.Period);
        var drafts = await PlanningKit.ListDraftsAsync(harness, setup.Period);

        // Assert
        Assert.Equal(5, drafts.Count(draft => draft.WorkerId == cy.ItemId));
    }

    [Fact]
    public async Task AutoSchedule_RefusesAScenarioThatDoesntExist_OrALockedSchedule()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().AutoScheduleAsync("missing", new AutoScheduleOptions(), "tester"));

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Coverage_ForASkill_CountsOnlyTheWorkersWhoHoldIt()
    {
        // Arrange (Ana and Ben work Monday; only Ana is a lead)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var lead = await PlanningKit.AddSkillAsync(harness, "Lead");
        var (ana, ben) = (setup.Members[0], setup.Members[1]);

        await PlanningKit.GrantSkillAsync(harness, ana.ItemId, lead.ItemId);
        await SaveRequirementAsync(harness, setup.Team, "Lead on duty", lead.ItemId, minimum: 1, target: 1);
        await PlanningKit.AddDraftAsync(harness, setup.Period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, setup.Period, ben.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        // Act
        var (skilled, everyone) = await harness.RunAsync(async services =>
        {
            var coverage = services.GetRequiredService<ICoverageService>();
            var query = new CoverageQuery { TeamId = setup.Team.ItemId, From = _monday, To = _monday, ScenarioId = setup.Period.PrimaryScenarioId };
            var all = await coverage.ComputeAsync(query);

            query.SkillId = lead.ItemId;

            return (await coverage.ComputeAsync(query), all);
        }, commit: false);

        // Assert (10:00-10:30)
        var at10 = PlanningKit.Utc(_monday, new TimeOnly(10, 0));

        Assert.Equal(1, skilled.Single(interval => interval.StartUtc == at10).Scheduled);
        Assert.Equal(2, everyone.Single(interval => interval.StartUtc == at10).Scheduled);
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, Action<WorkerProfile> configureBen = null)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var other = await WorkforceTests.AddTeamAsync(harness, "Lab", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team, configureBen);
        var cy = await PlanningKit.AddMemberAsync(harness, "Cy", team);

        // Neither of these may be scheduled: one is on another team, one has left.
        await PlanningKit.AddMemberAsync(harness, "Lab Worker", other);
        await PlanningKit.AddMemberAsync(harness, "Former", team, worker => worker.Status = WorkerStatus.Terminated);

        var day = await PlanningKit.AddTemplateAsync(harness, "Day", new TimeOnly(9, 0));

        // A template for another team only.
        await PlanningKit.AddTemplateAsync(harness, "Lab night", new TimeOnly(9, 0), configure: template => template.TeamIds = [other.ItemId]);
        await SaveRequirementAsync(harness, team, "Front desk", null, minimum: 1, target: 2);

        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        return new Setup(team, [ana, ben, cy], day, period);
    }

    private static Task SaveRequirementAsync(CalendarTestHarness harness, WorkforceTeam team, string name, string skillId, int minimum, int target)
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

    private static Task<AutoScheduleResult> AutoScheduleAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().AutoScheduleAsync(period.PrimaryScenarioId, new AutoScheduleOptions { TimeLimitSeconds = 10 }, "tester"));
    }

    private static Task<ScheduleScenario> FindScenarioAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ScheduleScenarioStore>().FindByIdAsync(period.PrimaryScenarioId).AsTask(), commit: false);
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile[] Members, ShiftTemplate Day, SchedulePeriod Period);
}
