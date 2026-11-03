using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Time-off grants: hours given with a last day to use them, used before the rest of the balance, and lost when the
/// day passes.
/// </summary>
public sealed class TimeOffGrantTests
{
    private static readonly DateOnly _grantStart = new(2026, 11, 1);
    private static readonly DateOnly _lastDay = new(2026, 12, 30);
    private static readonly DateOnly _dayAfter = new(2026, 12, 31);

    // Tuesdays.
    private static readonly DateOnly _beforeExpiry = new(2026, 12, 8);
    private static readonly DateOnly _afterExpiry = new(2027, 1, 5);

    [Fact]
    public async Task AGrant_IsUsedFirst_AndWhatIsLeftExpiresTheDayAfterItsLastDay()
    {
        // Arrange (16 hours from before, then 40 granted until 30 December; a day off on 8 December)
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness, opening: 16);
        var grant = await GrantAsync(harness, setup, 40);

        var taken = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _beforeExpiry);

        // Act
        var onLastDay = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _lastDay);
        var dayAfter = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _dayAfter);

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_dayAfter));
        await harness.RunAsync(services => services.GetRequiredService<ITimeOffAccrualService>().RunAsync(_dayAfter));

        var ledger = await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy);
        var expired = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _dayAfter);

        // Assert (the day off came out of the grant, so 32 hours of it are lost and the 16 stay)
        Assert.True(taken.Succeeded, string.Join(" ", taken.Errors));
        Assert.Equal(48m, onLastDay.Current);
        Assert.Equal(0m, onLastDay.Expiring);
        Assert.Equal(48m, onLastDay.Available);

        var remaining = Assert.Single(onLastDay.Grants);

        Assert.Equal(grant.ItemId, remaining.EntryId);
        Assert.Equal(32m, remaining.Remaining);
        Assert.Equal(_lastDay, remaining.ExpiresOn);
        Assert.Equal(32m, dayAfter.Expiring);
        Assert.Equal(16m, dayAfter.Available);

        var expiry = Assert.Single(ledger, entry => entry.Kind == TimeOffLedgerKind.Expiry);

        Assert.Equal(-32m, expiry.Amount);
        Assert.Equal(_dayAfter, expiry.EffectiveDate);
        Assert.Equal(grant.ItemId, expiry.SourceRef);
        Assert.Equal(16m, expired.Current);
        Assert.Equal(0m, expired.Expiring);
        Assert.Equal(16m, expired.Available);
    }

    [Fact]
    public async Task ARequestAfterAGrantExpires_CantCountOnIt_ButOneBeforeCan()
    {
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness, opening: 0);

        await GrantAsync(harness, setup, 40);

        var after = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _afterExpiry);
        var before = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _beforeExpiry);

        Assert.False(after.Succeeded);
        Assert.True(before.Succeeded, string.Join(" ", before.Errors));
    }

    [Fact]
    public async Task CancelingTimeOff_GivesItsHoursBackToTheGrant()
    {
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness, opening: 16);

        await GrantAsync(harness, setup, 40);

        var taken = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _beforeExpiry);
        var canceled = await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().CancelAsync(taken.Value.ItemId, "tester"));
        var balance = await TimeOffKit.BalanceAsync(harness, setup.Ana, setup.Policy, _dayAfter);

        Assert.True(canceled.Succeeded, string.Join(" ", canceled.Errors));
        Assert.Equal(40m, balance.Expiring);
        Assert.Equal(16m, balance.Available);
    }

    [Fact]
    public async Task AGrant_NeedsALastDay_OnOrAfterItStarts_AndAnAmountAbove0()
    {
        await using var harness = await TimeOffKit.CreateAsync();
        var setup = await SetUpAsync(harness, opening: 0);

        var noLastDay = await AdjustAsync(harness, setup, 40, null);
        var endsBefore = await AdjustAsync(harness, setup, 40, _grantStart.AddDays(-1));
        var negative = await AdjustAsync(harness, setup, -8, _lastDay);

        Assert.Equal("Give the grant the last day it can be used, on or after the day it starts.", Assert.Single(noLastDay.Errors));
        Assert.Equal("Give the grant the last day it can be used, on or after the day it starts.", Assert.Single(endsBefore.Errors));
        Assert.Equal("A grant adds hours or days; enter an amount above 0.", Assert.Single(negative.Errors));
    }

    private static async Task<TimeOffLedgerEntry> GrantAsync(CalendarTestHarness harness, Setup setup, decimal amount)
    {
        var result = await AdjustAsync(harness, setup, amount, _lastDay);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Value;
    }

    private static Task<PlanningResult<TimeOffLedgerEntry>> AdjustAsync(CalendarTestHarness harness, Setup setup, decimal amount, DateOnly? expiresOn)
        => harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(new TimeOffAdjustment
        {
            WorkerId = setup.Ana.ItemId,
            PolicyId = setup.Policy.ItemId,
            Kind = TimeOffLedgerKind.Grant,
            Amount = amount,
            EffectiveDate = _grantStart,
            ExpiresOn = expiresOn,
            Note = "Year-end bonus hours",
        }, "manager"));

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, decimal opening)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var vacation = await TimeOffKit.AddTypeAsync(harness, "Vacation");
        var policy = await TimeOffKit.AddPolicyAsync(harness, vacation, team);

        await TimeOffKit.AddAllotmentAsync(harness, team, slots: 2);

        if (opening != 0)
        {
            await TimeOffKit.OpenBalanceAsync(harness, ana, policy, opening);
        }

        return new Setup(team, ana, vacation, policy);
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Ana, TimeOffType Vacation, TimeOffPolicy Policy);
}
