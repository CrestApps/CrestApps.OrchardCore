using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Open shifts on a real SQLite store: publishing posts the shifts with nobody, a claim fills one by the team's claim
/// policy, a filled shift lands on the calendar and in the schedule so republishing leaves it alone, and a claim the
/// rules forbid is refused.
/// </summary>
public sealed class OpenShiftTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task Publish_PostsTheShiftsWithNobody_AndCancelsThemWhenTheyLeaveTheSchedule()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.ManagerApproves);

        // Act
        await PlanningKit.PublishAsync(harness, setup.Period);
        await PlanningKit.PublishAsync(harness, setup.Period);
        var posted = await ListAsync(harness, setup.Team);

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().DeleteDraftShiftAsync(setup.Open.ItemId, "tester"));
        await PlanningKit.PublishAsync(harness, setup.Period);
        var afterRemoval = await FindAsync(harness, Assert.Single(posted).ItemId);

        // Assert
        var shift = Assert.Single(posted);

        Assert.Equal(OpenShiftStatus.Open, shift.Status);
        Assert.Equal(setup.Open.ShiftKey, shift.ShiftKey);
        Assert.Equal(setup.Period.ItemId, shift.PeriodId);
        Assert.Equal(OpenShiftClaimPolicy.ManagerApproves, shift.ClaimPolicy);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(shift.Start));
        Assert.Equal([setup.Skill.ItemId], shift.RequiredSkillIds);
        Assert.Equal(OpenShiftStatus.Canceled, afterRemoval.Status);
        Assert.Empty(await ListAsync(harness, setup.Team));
    }

    [Fact]
    public async Task Claim_FirstCome_FillsTheShift_OnTheCalendarAndInTheSchedule()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.FirstCome);

        await PlanningKit.PublishAsync(harness, setup.Period);

        var posted = Assert.Single(await ListAsync(harness, setup.Team));

        // Act
        var claimed = await ClaimAsync(harness, posted, setup.Ana);
        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);
        var drafts = await PlanningKit.ListDraftsAsync(harness, setup.Period);
        var republished = await PlanningKit.PublishAsync(harness, setup.Period);
        var changes = await PlanningKit.ListChangesAsync(harness, setup.Period);

        // Assert
        Assert.True(claimed.Succeeded, string.Join(" ", claimed.Errors));
        Assert.Equal(OpenShiftStatus.Filled, claimed.Value.Status);
        Assert.Equal(setup.Ana.ItemId, claimed.Value.FilledWorkerId);

        var filled = Assert.Single(shifts, shift => shift.SourceId == posted.ShiftKey);

        Assert.Equal(setup.Ana.ItemId, PlanningKit.WorkerOf(filled));
        Assert.Equal(PlanningKit.Utc(_monday, new TimeOnly(9, 0)), filled.StartUtc);
        Assert.Equal(filled.ItemId, claimed.Value.CalendarEventId);
        Assert.Equal(setup.Ana.ItemId, drafts.Single(draft => draft.ShiftKey == posted.ShiftKey).WorkerId);

        // Republishing finds the filled shift already right.
        Assert.Equal((0, 0, 0, 0), (republished.Added, republished.Updated, republished.Removed, republished.Open));

        var change = Assert.Single(changes);

        Assert.Equal(ScheduleChangeKind.OpenShiftFilled, change.Kind);
        Assert.Equal(ScheduleChangeInitiator.Worker, change.InitiatedBy);
        Assert.Equal(setup.Ana.ItemId, change.WorkerId);
    }

    [Fact]
    public async Task Claim_ManagerApproves_WaitsForADecision_ThenDeclinesTheOthers()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.ManagerApproves);

        await PlanningKit.PublishAsync(harness, setup.Period);

        var posted = Assert.Single(await ListAsync(harness, setup.Team));

        // Act
        await ClaimAsync(harness, posted, setup.Ana);
        var claimed = await ClaimAsync(harness, posted, setup.Ben);
        var assigned = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().AssignAsync(posted.ItemId, setup.Ben.ItemId, "manager"));
        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);

        // Assert
        Assert.Equal(OpenShiftStatus.Claimed, claimed.Value.Status);
        Assert.Equal(2, claimed.Value.Claims.Count(claim => claim.Status == OpenShiftClaimStatus.Pending));
        Assert.True(assigned.Succeeded, string.Join(" ", assigned.Errors));
        Assert.Equal(OpenShiftStatus.Filled, assigned.Value.Status);
        Assert.Equal(OpenShiftClaimStatus.Approved, assigned.Value.Claims.Single(claim => claim.WorkerId == setup.Ben.ItemId).Status);
        Assert.Equal(OpenShiftClaimStatus.Declined, assigned.Value.Claims.Single(claim => claim.WorkerId == setup.Ana.ItemId).Status);
        Assert.Equal(setup.Ben.ItemId, PlanningKit.WorkerOf(Assert.Single(shifts, shift => shift.SourceId == posted.ShiftKey)));
    }

    [Fact]
    public async Task Claim_IsRefused_WhenTheWorkerCantTakeTheShift()
    {
        // Arrange (Cy lacks the skill; Ben works 12:00-16:00 that Monday; Dee rests too little after Sunday night;
        // Lab is on another team)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.FirstCome);
        var dee = await PlanningKit.AddMemberAsync(harness, "Dee", setup.Team);
        var lab = await PlanningKit.AddMemberAsync(harness, "Lab", await WorkforceTests.AddTeamAsync(harness, "Lab", TeamKind.Team));

        await PlanningKit.GrantSkillAsync(harness, dee.ItemId, setup.Skill.ItemId);
        await PlanningKit.GrantSkillAsync(harness, lab.ItemId, setup.Skill.ItemId);
        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ben.ItemId, _monday, new TimeOnly(12, 0), new TimeOnly(16, 0));
        await PlanningKit.PublishAsync(harness, setup.Period);

        // Dee's Sunday shift ends at 02:00 Monday, seven hours before the open shift.
        await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();
            var previous = await planning.CreatePeriodAsync(setup.Team.ItemId, _monday.AddDays(-7), 1, "Last week", "tester");

            await planning.SaveDraftShiftAsync(new DraftShift
            {
                ScenarioId = previous.Value.PrimaryScenarioId,
                WorkerId = dee.ItemId,
                Date = _monday.AddDays(-1),
                Start = _monday.AddDays(-1).ToDateTime(new TimeOnly(18, 0)),
                End = _monday.ToDateTime(new TimeOnly(2, 0)),
            }, "tester");
            await planning.PublishAsync(previous.Value.ItemId, "tester");
        });

        var posted = Assert.Single(await ListAsync(harness, setup.Team));

        // Act
        var cy = await CheckAsync(harness, posted, setup.Cy);
        var ben = await CheckAsync(harness, posted, setup.Ben);
        var deeProblems = await CheckAsync(harness, posted, dee);
        var labProblems = await CheckAsync(harness, posted, lab);
        var refused = await ClaimAsync(harness, posted, setup.Cy);
        var eligible = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().ListEligibleWorkersAsync(posted.ItemId), commit: false);

        // Assert
        Assert.Contains(cy, problem => problem.Contains("skill", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ben, problem => problem.Contains("overlap", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(deeProblems, problem => problem.Contains("rest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(labProblems, problem => problem.Contains("team", StringComparison.OrdinalIgnoreCase));
        Assert.False(refused.Succeeded);
        Assert.Equal(OpenShiftStatus.Open, (await FindAsync(harness, posted.ItemId)).Status);
        Assert.Equal([setup.Ana.ItemId], eligible.Select(worker => worker.ItemId));
    }

    [Fact]
    public async Task AwardDue_BySeniority_GivesTheShiftToTheMostSeniorClaimant_AtTheDeadline()
    {
        // Arrange (Ben has been here longer than Ana)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.BySeniority);

        await harness.RunAsync(async services =>
        {
            var workers = services.GetRequiredService<IWorkerManager>();

            foreach (var (worker, since) in new[] { (setup.Ana, new DateOnly(2022, 1, 1)), (setup.Ben, new DateOnly(2015, 6, 1)) })
            {
                var stored = await workers.FindByIdAsync(worker.ItemId);

                stored.SeniorityDate = since;
                await workers.UpdateAsync(stored);
            }
        });

        await PlanningKit.PublishAsync(harness, setup.Period);

        var posted = Assert.Single(await ListAsync(harness, setup.Team));

        await ClaimAsync(harness, posted, setup.Ana);
        await ClaimAsync(harness, posted, setup.Ben);

        // Act: nothing is due yet, then the deadline passes
        var early = await AwardDueAsync(harness);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<OpenShiftStore>();
            var stored = await store.FindByIdAsync(posted.ItemId);

            stored.ClaimDeadlineUtc = CalendarTestHarness.Now.AddMinutes(-1);
            await store.UpdateAsync(stored);
        });

        var awarded = await AwardDueAsync(harness);
        var shift = await FindAsync(harness, posted.ItemId);

        // Assert
        Assert.NotNull(posted.ClaimDeadlineUtc);
        Assert.Equal(0, early);
        Assert.Equal(1, awarded);
        Assert.Equal(OpenShiftStatus.Filled, shift.Status);
        Assert.Equal(setup.Ben.ItemId, shift.FilledWorkerId);
    }

    [Fact]
    public async Task Post_AnOpenShiftOnItsOwn_IsValidated_AndFillsLikeAScheduledOne()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.FirstCome);
        var tuesday = _monday.AddDays(1);

        // Act
        var (backwards, posted) = await harness.RunAsync(async services =>
        {
            var openShifts = services.GetRequiredService<IOpenShiftService>();
            var bad = await openShifts.PostAsync(new OpenShift { TeamId = setup.Team.ItemId, Date = tuesday, Start = tuesday.ToDateTime(new TimeOnly(17, 0)), End = tuesday.ToDateTime(new TimeOnly(9, 0)) }, "manager");
            var good = await openShifts.PostAsync(new OpenShift { TeamId = setup.Team.ItemId, Date = tuesday, Start = tuesday.ToDateTime(new TimeOnly(7, 0)), End = tuesday.ToDateTime(new TimeOnly(11, 0)), Notes = "Cover the morning" }, "manager");

            return (bad, good);
        });

        var claimed = await ClaimAsync(harness, posted.Value, setup.Cy);
        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);

        // Assert
        Assert.False(backwards.Succeeded);
        Assert.True(posted.Succeeded, string.Join(" ", posted.Errors));
        Assert.Null(posted.Value.PeriodId);
        Assert.Equal(OpenShiftClaimPolicy.FirstCome, posted.Value.ClaimPolicy);
        Assert.True(claimed.Succeeded, string.Join(" ", claimed.Errors));
        Assert.Equal(setup.Cy.ItemId, PlanningKit.WorkerOf(Assert.Single(shifts, shift => shift.SourceId == posted.Value.ShiftKey)));
    }

    [Fact]
    public async Task Withdraw_AndCancel_TakeTheShiftOffTheMarket()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, OpenShiftClaimPolicy.ManagerApproves);

        await PlanningKit.PublishAsync(harness, setup.Period);

        var posted = Assert.Single(await ListAsync(harness, setup.Team));

        await ClaimAsync(harness, posted, setup.Ana);

        // Act
        var withdrawn = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().WithdrawClaimAsync(posted.ItemId, setup.Ana.ItemId, "ana"));
        var canceled = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().CancelAsync(posted.ItemId, "manager"));
        var late = await ClaimAsync(harness, posted, setup.Ben);

        // Assert
        Assert.Equal(OpenShiftStatus.Open, withdrawn.Value.Status);
        Assert.Equal(OpenShiftClaimStatus.Withdrawn, Assert.Single(withdrawn.Value.Claims).Status);
        Assert.True(canceled);
        Assert.False(late.Succeeded);
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, OpenShiftClaimPolicy policy)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);

        team = await PlanningKit.UpdateTeamAsync(harness, team, stored => stored.OpenShiftClaimPolicy = policy);

        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var cy = await PlanningKit.AddMemberAsync(harness, "Cy", team);
        var skill = await PlanningKit.AddSkillAsync(harness, "Triage");

        await PlanningKit.GrantSkillAsync(harness, ana.ItemId, skill.ItemId);
        await PlanningKit.GrantSkillAsync(harness, ben.ItemId, skill.ItemId);

        var template = await PlanningKit.AddTemplateAsync(harness, "Triage", new TimeOnly(9, 0), configure: template => template.RequiredSkillIds = [skill.ItemId]);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);
        var open = await PlanningKit.AddDraftAsync(harness, period, null, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0), template.ItemId);

        return new Setup(team, ana, ben, cy, skill, template, period, open);
    }

    private static Task<IReadOnlyList<OpenShift>> ListAsync(CalendarTestHarness harness, WorkforceTeam team)
    {
        return harness.RunAsync(async services => (await services.GetRequiredService<IOpenShiftService>().PageAsync(new OpenShiftQuery { TeamIds = [team.ItemId] }, 0, 50)).Items, commit: false);
    }

    private static Task<OpenShift> FindAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().FindAsync(id), commit: false);
    }

    private static Task<PlanningResult<OpenShift>> ClaimAsync(CalendarTestHarness harness, OpenShift shift, WorkerProfile worker)
    {
        return harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().ClaimAsync(shift.ItemId, worker.ItemId, worker.UserId));
    }

    private static Task<IReadOnlyList<string>> CheckAsync(CalendarTestHarness harness, OpenShift shift, WorkerProfile worker)
    {
        return harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().CheckAsync(shift.ItemId, worker.ItemId), commit: false);
    }

    private static Task<int> AwardDueAsync(CalendarTestHarness harness)
    {
        return harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().AwardDueAsync());
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Ana, WorkerProfile Ben, WorkerProfile Cy, Skill Skill, ShiftTemplate Template, SchedulePeriod Period, DraftShift Open);
}
