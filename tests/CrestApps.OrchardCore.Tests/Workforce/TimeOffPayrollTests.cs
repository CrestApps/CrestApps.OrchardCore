using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Time off and payroll: a payout recorded on a balance becomes hours on the time-off type's pay code, paid once and
/// given back on a void, and pay statements show the balances as of the pay date.
/// </summary>
public sealed class TimeOffPayrollTests
{
    private static readonly PayInputQuery _period = new() { PeriodStart = new DateOnly(2026, 11, 8), PeriodEnd = new DateOnly(2026, 11, 21) };

    [Fact]
    public async Task Payouts_BecomeHoursOnTheTypesPayCode_InDaysTimesHoursPerDay_PaidOnce()
    {
        await using var harness = await TimeOffKit.CreateAsync(services =>
        {
            services.AddPaySetupCore();
            services.AddWorkforceTimeOffPayrollCore();
        });
        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team, worker => worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 22 }] }));
        var vacation = await TimeOffKit.AddTypeAsync(harness, "Vacation", type => type.PayCode = "PTO");
        var personal = await TimeOffKit.AddTypeAsync(harness, "Personal", type =>
        {
            type.Unit = TimeOffUnit.Days;
            type.HoursPerDay = 7.5m;
        });
        var vacationPolicy = await TimeOffKit.AddPolicyAsync(harness, vacation, team);
        var personalPolicy = await TimeOffKit.AddPolicyAsync(harness, personal, team);

        await TimeOffKit.OpenBalanceAsync(harness, ana, vacationPolicy, 40);
        await TimeOffKit.OpenBalanceAsync(harness, ana, personalPolicy, 3);
        await PayOutAsync(harness, ana, vacationPolicy, 16, new DateOnly(2026, 11, 10));
        await PayOutAsync(harness, ana, personalPolicy, 2, new DateOnly(2026, 11, 12));
        await PayOutAsync(harness, ana, vacationPolicy, 4, new DateOnly(2026, 11, 25));

        var (inputs, codes) = await harness.RunAsync(async services => (
            await services.GetRequiredService<IPayInputService>().GetInputsAsync(_period),
            (await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal)), commit: false);

        // The payout after the period waits for its run; the Personal type has no pay code, so it goes on PTO too.
        var payouts = inputs.Where(input => input.SourceKind == "TimeOffPayout").OrderBy(input => input.Date).ToList();

        Assert.Equal([("PTO", 16m, new DateOnly(2026, 11, 10)), ("PTO", 15m, new DateOnly(2026, 11, 12))], payouts.Select(input => (codes[input.PayCodeId], input.Hours.Value, input.Date.Value)));
        Assert.All(payouts, input => Assert.Equal((ana.ItemId, 22m), (input.StaffMemberId, input.Rate.Value)));

        // Taken by a run, they aren't offered again; a void gives them back.
        var consumption = new PayInputConsumption { ConsumerId = "run-1", Sources = [.. payouts.Select(input => new PayInputReference(input.SourceKind, input.SourceId))] };

        await harness.RunAsync(services => services.GetRequiredService<IPayInputService>().MarkConsumedAsync(consumption));
        Assert.DoesNotContain(await harness.RunAsync(services => services.GetRequiredService<IPayInputService>().GetInputsAsync(_period), commit: false), input => input.SourceKind == "TimeOffPayout");

        await harness.RunAsync(services => services.GetRequiredService<IPayInputService>().ReleaseAsync(consumption));
        Assert.Equal(2, (await harness.RunAsync(services => services.GetRequiredService<IPayInputService>().GetInputsAsync(_period), commit: false)).Count(input => input.SourceKind == "TimeOffPayout"));

        // The stub shows what is left of each type as of the pay date.
        var balances = await harness.RunAsync(async services =>
        {
            var all = new List<PayStatementBalance>();

            foreach (var source in services.GetServices<IPayStatementBalanceSource>())
            {
                all.AddRange(await source.GetBalancesAsync(ana.ItemId, new DateOnly(2026, 11, 20)));
            }

            return all;
        }, commit: false);

        Assert.Equal([("Personal", PayStatementBalance.Days, 1m), ("Vacation", PayStatementBalance.Hours, 24m)], balances.Select(balance => (balance.Name, balance.Unit, balance.Balance)));
    }

    private static Task PayOutAsync(Calendars.CalendarTestHarness harness, StaffMember worker, TimeOffPolicy policy, decimal amount, DateOnly date)
        => harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(new TimeOffAdjustment
            {
                WorkerId = worker.ItemId,
                PolicyId = policy.ItemId,
                Kind = TimeOffLedgerKind.Payout,
                Amount = amount,
                EffectiveDate = date,
            }, "manager");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        });
}
