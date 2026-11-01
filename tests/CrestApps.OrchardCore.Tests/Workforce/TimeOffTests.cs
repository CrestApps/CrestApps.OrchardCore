using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Time off on a real SQLite store: accrual that is written once however often it runs, what a request counts,
/// the checks a request must pass, allotments that approve on their own, two requests racing for the last place,
/// the waitlist taking a freed place, decisions, the shifts approved time off overlaps, pending requests on the team
/// calendar, and balance adjustments.
/// </summary>
public sealed class TimeOffTests
{
    private static readonly DateOnly _today = new(2026, 11, 2);
    private static readonly DateOnly _friday = new(2026, 11, 6);
    private static readonly DateOnly _monday = PlanningKit.Monday;
    private static readonly DateOnly _tuesday = PlanningKit.Monday.AddDays(1);

    [Fact]
    public async Task Accrual_IsWrittenOnce_HoweverOftenItRuns()
    {
        // Arrange (8 hours a month from June)
        await using var harness = await TimeOffKit.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team, worker => worker.HireDate = new DateOnly(2026, 6, 1));
        var type = await TimeOffKit.AddTypeAsync(harness, "Vacation");
        var policy = await TimeOffKit.AddPolicyAsync(harness, type, team, policy =>
        {
            policy.AccrualMethod = TimeOffAccrualMethod.PerPeriod;
            policy.Rate = 8;
        });

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_today));
        var second = await harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_today));
        var together = await Task.WhenAll(
            harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_today)),
            harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_today)));
        var ledger = await TimeOffKit.LedgerAsync(harness, ana, policy);
        var balance = await TimeOffKit.BalanceAsync(harness, ana, policy, _today);

        // Assert (June to October)
        Assert.Equal(5, first.Entries);
        Assert.Equal(0, second.Entries);
        Assert.All(together, run => Assert.Equal(0, run.Entries));
        Assert.Equal(5, ledger.Count);
        Assert.All(ledger, entry => Assert.Equal(TimeOffLedgerKind.Accrual, entry.Kind));
        Assert.Equal(40m, balance.Current);
    }

    [Fact]
    public async Task Preview_CountsAShiftsPaidHours_AndTheDayLength_OnWeekdaysWithoutAShift()
    {
        // Arrange (Ana works 9:00 to 16:30 on Monday)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(16, 30));
        await PlanningKit.PublishAsync(harness, period);

        // Act (Friday to Monday)
        var preview = await TimeOffKit.PreviewAsync(harness, setup.Ana, setup.Vacation, _friday, _monday);

        // Assert
        Assert.Empty(preview.Problems);
        Assert.Equal([8m, 0m, 0m, 7.5m], preview.Days.Select(day => day.Amount));
        Assert.Equal(15.5m, preview.Amount);
        Assert.Equal(1, preview.OverlappingShifts);
        Assert.Equal(40m, preview.Balance.Available);
        Assert.Equal(TimeOffRequestStatus.Pending, preview.Outcome);
        Assert.Equal(setup.Team.ItemId, preview.TeamId);
    }

    [Fact]
    public async Task Preview_PartOfADay_CountsItsHours_OrItsShareOfADay()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var personal = await TimeOffKit.AddTypeAsync(harness, "Personal", type =>
        {
            type.Unit = TimeOffUnit.Days;
            type.RequiresBalance = false;
        });

        // Act
        var hours = await TimeOffKit.PreviewAsync(harness, setup.Ana, setup.Vacation, _tuesday, configure: Afternoon);
        var days = await TimeOffKit.PreviewAsync(harness, setup.Ana, personal, _tuesday, configure: Afternoon);

        // Assert
        Assert.Empty(hours.Problems);
        Assert.Equal(4m, hours.Amount);
        Assert.Equal(240, Assert.Single(hours.Days).Minutes);
        Assert.Empty(days.Problems);
        Assert.Equal(0.5m, days.Amount);
        Assert.Null(days.Policy);
    }

    [Fact]
    public async Task Submit_IsRefused_InABlackout_WithTooLittleBalance_OverAnotherRequest_OrWithoutAReason()
    {
        // Arrange (Cy has 4 hours; sick time needs a reason)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness, cyBalance: 4);
        var sick = await TimeOffKit.AddTypeAsync(harness, "Sick", type =>
        {
            type.RequiresBalance = false;
            type.RequiresReason = true;
        });

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IBlackoutPeriodManager>();
            var blackout = await manager.NewAsync();

            blackout.Name = "Year-end close";
            blackout.TeamId = setup.Team.ItemId;
            blackout.StartDate = new DateOnly(2026, 11, 23);
            blackout.EndDate = new DateOnly(2026, 11, 27);
            blackout.Message = "No time off during the year-end close.";
            await manager.CreateAsync(blackout);
        });

        var first = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);

        // Act
        var inBlackout = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, new DateOnly(2026, 11, 24));
        var tooLittle = await TimeOffKit.SubmitAsync(harness, setup.Cy, setup.Vacation, _tuesday);
        var overlapping = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _monday, _tuesday);
        var noReason = await TimeOffKit.SubmitAsync(harness, setup.Ben, sick, _tuesday);

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.Contains(inBlackout.Errors, error => error.Contains("year-end close", StringComparison.Ordinal));
        Assert.Contains(tooLittle.Errors, error => error.Contains("balance", StringComparison.Ordinal));
        Assert.Contains(overlapping.Errors, error => error.Contains("already", StringComparison.Ordinal));
        Assert.Contains(noReason.Errors, error => error.Contains("reason", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Submit_WithinAnAllotmentThatApproves_IsApproved_OnTheCalendar_AndTakenFromTheBalance()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);

        await TimeOffKit.AddAllotmentAsync(harness, setup.Team, slots: 2);

        // Act
        var submitted = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);
        var ledger = await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy);
        var balance = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _today);
        var calendar = await TimeOffKit.ListTimeOffAsync(harness, setup.Team);

        // Assert
        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));
        Assert.Equal(TimeOffRequestStatus.Approved, submitted.Value.Status);
        Assert.True(submitted.Value.AutoApproved);

        var taken = Assert.Single(ledger, entry => entry.Kind == TimeOffLedgerKind.Taken);

        Assert.Equal(-8m, taken.Amount);
        Assert.Equal(_tuesday, taken.EffectiveDate);
        Assert.Equal(40m, balance.Current);
        Assert.Equal(8m, balance.Upcoming);
        Assert.Equal(32m, balance.Available);

        var timeOff = Assert.Single(calendar);

        Assert.Equal(submitted.Value.CalendarEventId, timeOff.Event.ItemId);
        Assert.Equal(CalendarEventStatus.Confirmed, timeOff.Event.Status);
        Assert.True(timeOff.Event.AllDay);
        Assert.False(timeOff.Event.Editable);
        Assert.Equal(WorkforceConstants.Source, timeOff.Event.Source);
        Assert.Contains("Ana", timeOff.Event.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastPlace_TwoRequestsAtOnce_OneIsApproved_AndTheOtherWaits()
    {
        // Arrange (one person may be off)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);

        await TimeOffKit.AddAllotmentAsync(harness, setup.Team, slots: 1);

        // Act
        var results = await Task.WhenAll(
            TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday),
            TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday));
        var calendar = await TimeOffKit.ListTimeOffAsync(harness, setup.Team);

        // Assert
        Assert.All(results, result => Assert.True(result.Succeeded, string.Join(" ", result.Errors)));
        Assert.Equal(
            [TimeOffRequestStatus.Waitlisted, TimeOffRequestStatus.Approved],
            results.Select(result => result.Value.Status).Order());
        Assert.Single(calendar, occurrence => occurrence.Event.Status == CalendarEventStatus.Confirmed);
    }

    [Fact]
    public async Task Cancel_ApprovedTimeOff_GivesTheBalanceBack_AndThePlaceToTheFirstWaiting()
    {
        // Arrange (Ana has the place; Ben then Cy wait for it)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);

        await TimeOffKit.AddAllotmentAsync(harness, setup.Team, slots: 1);

        var ana = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);
        var ben = await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday);
        var cy = await TimeOffKit.SubmitAsync(harness, setup.Cy, setup.Vacation, _tuesday);

        // Act
        var canceled = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().CancelAsync(ana.Value.ItemId, "manager", "Plans changed"));
        var promoted = await TimeOffKit.FindAsync(harness, ben.Value.ItemId);
        var stillWaiting = await TimeOffKit.FindAsync(harness, cy.Value.ItemId);
        var anaLedger = await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy);
        var anaBalance = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _today);
        var calendar = await TimeOffKit.ListTimeOffAsync(harness, setup.Team);

        // Assert
        Assert.Equal(TimeOffRequestStatus.Waitlisted, ben.Value.Status);
        Assert.True(canceled.Succeeded, string.Join(" ", canceled.Errors));
        Assert.Equal(TimeOffRequestStatus.Canceled, canceled.Value.Status);
        Assert.Equal([TimeOffLedgerKind.Opening, TimeOffLedgerKind.Taken, TimeOffLedgerKind.Reversal], anaLedger.Select(entry => entry.Kind).Order());
        Assert.Equal(40m, anaBalance.Available);
        Assert.Equal(TimeOffRequestStatus.Approved, promoted.Status);
        Assert.True(promoted.AutoApproved);
        Assert.Equal(TimeOffRequestStatus.Waitlisted, stillWaiting.Status);

        var live = Assert.Single(calendar, occurrence => occurrence.Event.Status == CalendarEventStatus.Confirmed);

        Assert.Equal(promoted.CalendarEventId, live.Event.ItemId);
    }

    [Fact]
    public async Task WithoutAnAllotment_ARequestWaitsForAManager_WhoApprovesOrDeclines_AndAnOpenRequestCanBeWithdrawn()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var ana = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);
        var ben = await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday.AddDays(1));
        var cy = await TimeOffKit.SubmitAsync(harness, setup.Cy, setup.Vacation, _tuesday.AddDays(2));

        // Act
        var approved = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(ana.Value.ItemId, "manager", "Enjoy"));
        var declined = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().DeclineAsync(ben.Value.ItemId, "manager", "Inventory week"));
        var withdrawn = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().CancelAsync(cy.Value.ItemId, "Cy"));
        var again = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(ben.Value.ItemId, "manager"));

        // Assert
        Assert.Equal(TimeOffRequestStatus.Pending, ana.Value.Status);
        Assert.Equal(TimeOffRequestStatus.Approved, approved.Value.Status);
        Assert.False(approved.Value.AutoApproved);
        Assert.Equal("manager", approved.Value.DecidedBy);
        Assert.NotNull(approved.Value.CalendarEventId);
        Assert.Contains(await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy), entry => entry.Kind == TimeOffLedgerKind.Taken);
        Assert.Equal(TimeOffRequestStatus.Declined, declined.Value.Status);
        Assert.Equal("Inventory week", declined.Value.DecisionNote);
        Assert.DoesNotContain(await TimeOffKit.LedgerAsync(harness, setup.Ben, setup.Policy), entry => entry.Kind == TimeOffLedgerKind.Taken);
        Assert.Equal(TimeOffRequestStatus.Withdrawn, withdrawn.Value.Status);
        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task FullAllotment_SendsTheRequestToAManager_WhenItIsSetTo()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);

        await TimeOffKit.AddAllotmentAsync(harness, setup.Team, slots: 1, allotment => allotment.WhenFull = TimeOffAllotmentFullAction.Pending);
        await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);

        // Act
        var preview = await TimeOffKit.PreviewAsync(harness, setup.Ben, setup.Vacation, _tuesday);
        var ben = await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday);
        var approved = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(ben.Value.ItemId, "manager"));

        // Assert
        Assert.Equal([_tuesday], preview.FullDates);
        Assert.Equal(TimeOffRequestStatus.Pending, preview.Outcome);
        Assert.Equal(TimeOffRequestStatus.Pending, ben.Value.Status);
        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.Equal(TimeOffRequestStatus.Approved, approved.Value.Status);
    }

    [Fact]
    public async Task Approve_OverAPublishedShift_FlagsIt_OrRemovesIt_WhenTheTeamSaysSo()
    {
        // Arrange (Ana and Ben both work Monday)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, setup.Ben.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);
        var anaShift = shifts.Single(shift => PlanningKit.WorkerOf(shift) == setup.Ana.ItemId);
        var anaRequest = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _monday);
        var benRequest = await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _monday);

        // Act
        var flagged = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(anaRequest.Value.ItemId, "manager"));

        await PlanningKit.UpdateTeamAsync(harness, setup.Team, team => team.TimeOffShiftHandling = TimeOffShiftHandling.Remove);

        var removed = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(benRequest.Value.ItemId, "manager"));
        var after = await PlanningKit.ListShiftsAsync(harness, setup.Team);
        var drafts = await PlanningKit.ListDraftsAsync(harness, period);
        var changes = await PlanningKit.ListChangesAsync(harness, period);

        // Assert
        Assert.Equal([anaShift.ItemId], flagged.Value.FlaggedShiftEventIds);
        Assert.Empty(removed.Value.FlaggedShiftEventIds);
        Assert.Equal([setup.Ana.ItemId], after.Select(PlanningKit.WorkerOf));
        Assert.DoesNotContain(drafts, draft => draft.WorkerId == setup.Ben.ItemId);

        var change = Assert.Single(changes);

        Assert.Equal(ScheduleChangeKind.Removed, change.Kind);
        Assert.Equal(ScheduleChangeInitiator.Worker, change.InitiatedBy);
        Assert.Equal(setup.Ben.ItemId, change.WorkerId);
        Assert.NotNull(change.Reason);
    }

    [Fact]
    public async Task PendingRequests_ShowOnTheTeamCalendar_AsTentative_UntilDecided()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var request = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday, _tuesday.AddDays(1));

        // Act
        var pending = await TimeOffKit.ListTimeOffAsync(harness, setup.Team);

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(request.Value.ItemId, "manager"));

        var approved = await TimeOffKit.ListTimeOffAsync(harness, setup.Team);

        // Assert
        var tentative = Assert.Single(pending);

        Assert.Equal(CalendarEventStatus.Tentative, tentative.Event.Status);
        Assert.False(tentative.Event.Editable);
        Assert.True(tentative.Event.AllDay);
        Assert.Equal(CalendarTransparency.Free, tentative.Event.Transparency);
        Assert.Equal(PlanningKit.Utc(_tuesday, TimeOnly.MinValue), tentative.StartUtc);
        Assert.Equal(PlanningKit.Utc(_tuesday.AddDays(2), TimeOnly.MinValue), tentative.EndUtc);

        var confirmed = Assert.Single(approved);

        Assert.Equal(CalendarEventStatus.Confirmed, confirmed.Event.Status);
    }

    [Fact]
    public async Task Adjust_WithAKey_IsMadeOnce_AndAPayoutTakesAway()
    {
        // Arrange
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness);
        var adjustment = new TimeOffAdjustment
        {
            WorkerId = setup.Ana.ItemId,
            PolicyId = setup.Policy.ItemId,
            Kind = TimeOffLedgerKind.Adjustment,
            Amount = 5,
            EffectiveDate = _today,
            IdempotencyKey = "import-1",
        };

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(adjustment, "manager"));
        var second = await harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(adjustment, "manager"));
        var payout = await harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(new TimeOffAdjustment
        {
            WorkerId = setup.Ana.ItemId,
            PolicyId = setup.Policy.ItemId,
            Kind = TimeOffLedgerKind.Payout,
            Amount = 3,
            EffectiveDate = _today,
        }, "manager"));
        var ledger = await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy);
        var balance = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _today);

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.Equal(first.Value.ItemId, second.Value.ItemId);
        Assert.Equal(-3m, payout.Value.Amount);
        Assert.Equal(3, ledger.Count);
        Assert.Equal(42m, balance.Current);
    }

    private static void Afternoon(TimeOffRequestInput input)
    {
        input.StartTime = new TimeOnly(13, 0);
        input.EndTime = new TimeOnly(17, 0);
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, decimal cyBalance = 40)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);
        var cy = await PlanningKit.AddMemberAsync(harness, "Cy", team);
        var vacation = await TimeOffKit.AddTypeAsync(harness, "Vacation");
        var policy = await TimeOffKit.AddPolicyAsync(harness, vacation, team);

        await TimeOffKit.OpenBalanceAsync(harness, ana, policy, 40);
        await TimeOffKit.OpenBalanceAsync(harness, ben, policy, 40);
        await TimeOffKit.OpenBalanceAsync(harness, cy, policy, cyBalance);

        return new Setup(team, ana, ben, cy, vacation, policy);
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Ana, WorkerProfile Ben, WorkerProfile Cy, TimeOffType Vacation, TimeOffPolicy Policy);
}
