using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Shift trades on a real SQLite store: a give-away a manager approves, a swap the team lets through on acceptance, a
/// pick-up by anyone eligible, the rules that refuse a trade, and the schedule's draft kept in step so republishing
/// leaves a traded shift alone.
/// </summary>
public sealed class ShiftTradeTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task GiveAway_AcceptedByTheColleague_ThenApproved_MovesTheShift()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: true);

        // Act
        var offered = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId, TargetWorkerId = setup.Ben.ItemId, Note = "Dentist" });
        var accepted = await AcceptAsync(harness, offered.Value, setup.Ben);
        var beforeApproval = await ShiftAsync(harness, setup, setup.AnaMonday);
        var approved = await harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().ApproveAsync(offered.Value.ItemId, "manager"));
        var afterApproval = await ShiftAsync(harness, setup, setup.AnaMonday);
        var draft = (await PlanningKit.ListDraftsAsync(harness, setup.Period)).Single(draft => draft.ShiftKey == setup.AnaMonday.SourceId);
        var republished = await PlanningKit.PublishAsync(harness, setup.Period);
        var changes = await PlanningKit.ListChangesAsync(harness, setup.Period);

        // Assert
        Assert.True(offered.Succeeded, string.Join(" ", offered.Errors));
        Assert.Equal(ShiftTradeStatus.Offered, offered.Value.Status);
        Assert.True(offered.Value.RequiresApproval);
        Assert.Equal(ShiftTradeStatus.Accepted, accepted.Value.Status);
        Assert.Equal(setup.Ana.ItemId, PlanningKit.WorkerOf(beforeApproval));
        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.Equal(ShiftTradeStatus.Approved, approved.Value.Status);
        Assert.Equal(setup.Ben.ItemId, PlanningKit.WorkerOf(afterApproval));
        Assert.Equal(setup.Ben.UserId, Assert.Single(afterApproval.Attendees).UserId);
        Assert.Equal(setup.Ben.ItemId, draft.WorkerId);
        Assert.Equal((0, 0, 0), (republished.Added, republished.Updated, republished.Removed));

        var change = Assert.Single(changes);

        Assert.Equal(ScheduleChangeKind.Reassigned, change.Kind);
        Assert.Equal(ScheduleChangeInitiator.Worker, change.InitiatedBy);
        Assert.Equal(setup.Ana.ItemId, change.PreviousWorkerId);
        Assert.Equal(setup.Ben.ItemId, change.WorkerId);
    }

    [Fact]
    public async Task Swap_WhenTheTeamDoesntNeedApproval_IsAppliedWhenAccepted()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: false);

        // Act
        var offered = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.Swap, ShiftEventId = setup.AnaMonday.ItemId, TargetWorkerId = setup.Ben.ItemId, TargetShiftEventId = setup.BenTuesday.ItemId });
        var accepted = await AcceptAsync(harness, offered.Value, setup.Ben);
        var monday = await ShiftAsync(harness, setup, setup.AnaMonday);
        var tuesday = await ShiftAsync(harness, setup, setup.BenTuesday);
        var changes = await PlanningKit.ListChangesAsync(harness, setup.Period);

        // Assert
        Assert.True(accepted.Succeeded, string.Join(" ", accepted.Errors));
        Assert.Equal(ShiftTradeStatus.Approved, accepted.Value.Status);
        Assert.False(accepted.Value.RequiresApproval);
        Assert.Equal(setup.Ben.ItemId, PlanningKit.WorkerOf(monday));
        Assert.Equal(setup.Ana.ItemId, PlanningKit.WorkerOf(tuesday));
        Assert.Equal(2, changes.Count(change => change.Kind == ScheduleChangeKind.Swapped));
    }

    [Fact]
    public async Task GiveAway_ToAnyone_IsPickedUpByAnEligibleWorker()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: false);

        // Act
        var offered = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId });
        var byAna = await AcceptAsync(harness, offered.Value, setup.Ana);
        var byCy = await AcceptAsync(harness, offered.Value, setup.Cy);

        // Assert
        Assert.False(byAna.Succeeded);
        Assert.True(byCy.Succeeded, string.Join(" ", byCy.Errors));
        Assert.Equal(setup.Cy.ItemId, byCy.Value.AcceptedByWorkerId);
        Assert.Equal(setup.Cy.ItemId, PlanningKit.WorkerOf(await ShiftAsync(harness, setup, setup.AnaMonday)));
    }

    [Fact]
    public async Task Accept_IsRefused_WhenTheColleagueWouldRestTooLittle_OrLacksTheSkill()
    {
        // Arrange (Ben works Monday 19:00-23:00, two hours after Ana's shift ends; Cy lacks the template's skill)
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: true, skilled: true);

        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ben.ItemId, _monday, new TimeOnly(19, 0), new TimeOnly(23, 0));
        await PlanningKit.PublishAsync(harness, setup.Period);

        var offered = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId });

        // Act
        var ben = await harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().CheckAsync(offered.Value.ItemId, setup.Ben.ItemId), commit: false);
        var cy = await harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().CheckAsync(offered.Value.ItemId, setup.Cy.ItemId), commit: false);
        var refused = await AcceptAsync(harness, offered.Value, setup.Ben);

        // Assert
        Assert.Contains(ben, problem => problem.Contains("rest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cy, problem => problem.Contains("skill", StringComparison.OrdinalIgnoreCase));
        Assert.False(refused.Succeeded);
        Assert.Equal(ShiftTradeStatus.Offered, (await FindAsync(harness, offered.Value)).Status);
        Assert.Equal(setup.Ana.ItemId, PlanningKit.WorkerOf(await ShiftAsync(harness, setup, setup.AnaMonday)));
    }

    [Fact]
    public async Task Offer_IsRefused_ForASwapOfSomeoneElsesShift_OrAShiftAlreadyOffered()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: true);

        // Act
        var wrongTarget = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.Swap, ShiftEventId = setup.AnaMonday.ItemId, TargetWorkerId = setup.Cy.ItemId, TargetShiftEventId = setup.BenTuesday.ItemId });
        var noTarget = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.Swap, ShiftEventId = setup.AnaMonday.ItemId });
        var first = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId });
        var second = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId });
        var missing = await OfferAsync(harness, new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = "missing" });

        // Assert
        Assert.False(wrongTarget.Succeeded);
        Assert.False(noTarget.Succeeded);
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(second.Succeeded);
        Assert.False(missing.Succeeded);
    }

    [Fact]
    public async Task Reject_Decline_AndCancel_EndTheTrade_WithoutMovingTheShift()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness, needsApproval: true);

        // Act
        var rejected = await harness.RunAsync(async services =>
        {
            var trades = services.GetRequiredService<IShiftTradeService>();
            var offer = await trades.OfferAsync(new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId, TargetWorkerId = setup.Ben.ItemId }, "ana");

            return await trades.RejectAsync(offer.Value.ItemId, setup.Ben.ItemId, "ben");
        });

        var declined = await harness.RunAsync(async services =>
        {
            var trades = services.GetRequiredService<IShiftTradeService>();
            var offer = await trades.OfferAsync(new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId, TargetWorkerId = setup.Ben.ItemId }, "ana");

            await trades.AcceptAsync(offer.Value.ItemId, setup.Ben.ItemId, "ben");

            return await trades.DeclineAsync(offer.Value.ItemId, "We need Ana that day.", "manager");
        });

        var canceled = await harness.RunAsync(async services =>
        {
            var trades = services.GetRequiredService<IShiftTradeService>();
            var offer = await trades.OfferAsync(new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = setup.AnaMonday.ItemId }, "ana");

            return await trades.CancelAsync(offer.Value.ItemId, "ana");
        });

        var lateAccept = await AcceptAsync(harness, canceled.Value, setup.Cy);

        // Assert
        Assert.Equal(ShiftTradeStatus.Rejected, rejected.Value.Status);
        Assert.Equal(ShiftTradeStatus.Declined, declined.Value.Status);
        Assert.Equal("We need Ana that day.", declined.Value.DecisionNote);
        Assert.Equal(ShiftTradeStatus.Canceled, canceled.Value.Status);
        Assert.False(lateAccept.Succeeded);
        Assert.Equal(setup.Ana.ItemId, PlanningKit.WorkerOf(await ShiftAsync(harness, setup, setup.AnaMonday)));
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, bool needsApproval, bool skilled = false)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);

        team = await PlanningKit.UpdateTeamAsync(harness, team, stored => stored.ShiftTradesNeedApproval = needsApproval);

        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var cy = await PlanningKit.AddMemberAsync(harness, "Cy", team);
        string skillId = null;

        if (skilled)
        {
            var skill = await PlanningKit.AddSkillAsync(harness, "Triage");

            skillId = skill.ItemId;
            await PlanningKit.GrantSkillAsync(harness, ana.ItemId, skillId);
            await PlanningKit.GrantSkillAsync(harness, ben.ItemId, skillId);
        }

        var template = await PlanningKit.AddTemplateAsync(harness, "Day", new TimeOnly(9, 0), configure: template => template.RequiredSkillIds = skillId is null ? [] : [skillId]);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0), template.ItemId);
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday.AddDays(1), new TimeOnly(9, 0), new TimeOnly(17, 0), template.ItemId);
        await PlanningKit.PublishAsync(harness, period);

        var shifts = await PlanningKit.ListShiftsAsync(harness, team);

        return new Setup(
            team,
            ana,
            ben,
            cy,
            period,
            shifts.Single(shift => PlanningKit.WorkerOf(shift) == ana.ItemId),
            shifts.Single(shift => PlanningKit.WorkerOf(shift) == ben.ItemId));
    }

    private static Task<PlanningResult<ShiftTrade>> OfferAsync(CalendarTestHarness harness, ShiftTradeOffer offer)
    {
        return harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().OfferAsync(offer, "tester"));
    }

    private static Task<PlanningResult<ShiftTrade>> AcceptAsync(CalendarTestHarness harness, ShiftTrade trade, WorkerProfile worker)
    {
        return harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().AcceptAsync(trade.ItemId, worker.ItemId, worker.UserId));
    }

    private static Task<ShiftTrade> FindAsync(CalendarTestHarness harness, ShiftTrade trade)
    {
        return harness.RunAsync(services => services.GetRequiredService<IShiftTradeService>().FindAsync(trade.ItemId), commit: false);
    }

    private static async Task<CalendarEvent> ShiftAsync(CalendarTestHarness harness, Setup setup, CalendarEvent shift)
    {
        return (await PlanningKit.ListShiftsAsync(harness, setup.Team)).Single(calendarEvent => calendarEvent.ItemId == shift.ItemId);
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Ana, WorkerProfile Ben, WorkerProfile Cy, SchedulePeriod Period, CalendarEvent AnaMonday, CalendarEvent BenTuesday);
}
