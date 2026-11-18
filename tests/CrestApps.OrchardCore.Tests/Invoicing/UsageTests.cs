using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Usage is recorded once per event, added up as its meter says, priced by graduated or volume tiers, and billed by a
/// recurring plan for the period that just ended.
/// </summary>
public sealed class UsageTests
{
    private static readonly PartyRef _customer = new(PartyKinds.User, "user-1");
    private static readonly PartyRef _other = new(PartyKinds.User, "user-2");

    private static readonly UsagePriceTier[] _tiers =
    [
        new() { UpTo = 1000m, UnitPrice = 0.10m },
        new() { UpTo = 5000m, UnitPrice = 0.05m },
        new() { UpTo = null, UnitPrice = 0.01m },
    ];

    [Fact]
    public async Task AnEvent_IsRecordedOnce_AndAMeterThatIsOffOrUnknownTakesNothing()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await CreateMeterAsync(harness, "API calls", "api-calls", UsageAggregation.Sum);
        await CreateMeterAsync(harness, "Old meter", "old", UsageAggregation.Sum, enabled: false);

        // Act
        var first = await RecordAsync(harness, "API-Calls", _customer, 40m, "evt-1");
        var again = await RecordAsync(harness, "api-calls", _customer, 40m, "evt-1");
        var off = await RecordAsync(harness, "old", _customer, 1m, "evt-2");
        var unknown = await RecordAsync(harness, "nothing", _customer, 1m, "evt-3");
        var negative = await RecordAsync(harness, "api-calls", _customer, -5m, "evt-4");

        // Assert
        Assert.True(first.Succeeded && !first.AlreadyRecorded, string.Join(" ", first.Errors));
        Assert.True(again.Succeeded && again.AlreadyRecorded);
        Assert.Equal(first.Record.ItemId, again.Record.ItemId);
        Assert.False(off.Succeeded || unknown.Succeeded || negative.Succeeded);
        Assert.Equal(40m, await TotalAsync(harness, "api-calls", _customer, new DateTime(2026, 1, 1), new DateTime(2027, 1, 1)));
    }

    [Theory]
    [InlineData(UsageAggregation.Sum, 13)]
    [InlineData(UsageAggregation.Maximum, 8)]
    [InlineData(UsageAggregation.Last, 2)]
    public async Task ThePeriodsUsage_IsAddedUpAsItsMeterSays_ForTheCustomerAndPeriodOnly(UsageAggregation aggregation, int expected)
    {
        // Arrange: 3, 8 and 2 in November (in that order), one outside it and one of another customer.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await CreateMeterAsync(harness, "Seats", "seats", aggregation);
        await RecordAsync(harness, "seats", _customer, 3m, "a", new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "seats", _customer, 8m, "b", new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "seats", _customer, 2m, "c", new DateTime(2026, 11, 29, 9, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "seats", _customer, 50m, "d", new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "seats", _other, 70m, "e", new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc));

        // Act
        var total = await TotalAsync(harness, "seats", _customer, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(expected, total);
    }

    [Fact]
    public void GraduatedTiers_PriceEachBandAtItsOwnPrice_AndVolumeTiersPriceAllOfItAtOne()
    {
        // Act
        var graduated = UsagePricing.Price(UsageTierMode.Graduated, _tiers, 7000m);
        var small = UsagePricing.Price(UsageTierMode.Graduated, _tiers, 600m);
        var volume = UsagePricing.Price(UsageTierMode.Volume, _tiers, 3000m);
        var none = UsagePricing.Price(UsageTierMode.Graduated, _tiers, 0m);
        var past = UsagePricing.Price(UsageTierMode.Graduated, [new UsagePriceTier { UpTo = 100m, UnitPrice = 1m }], 150m);

        // Assert: 1,000 × 0.10 + 4,000 × 0.05 + 2,000 × 0.01 = 320; 3,000 × 0.05 = 150.
        Assert.Equal([(1000m, 0.10m), (4000m, 0.05m), (2000m, 0.01m)], graduated.Select(band => (band.Quantity, band.UnitPrice)));
        Assert.Equal(320m, graduated.Sum(band => band.Quantity * band.UnitPrice));
        Assert.Equal([(600m, 0.10m)], small.Select(band => (band.Quantity, band.UnitPrice)));
        Assert.Equal([(3000m, 0.05m)], volume.Select(band => (band.Quantity, band.UnitPrice)));
        Assert.Empty(none);
        Assert.Equal(150m, past.Sum(band => band.Quantity * band.UnitPrice));
        Assert.NotEmpty(UsagePricing.Validate([new UsagePriceTier { UpTo = 500m }, new UsagePriceTier { UpTo = 100m }], new PassThroughStringLocalizer<UsageTests>()));
        Assert.NotEmpty(UsagePricing.Validate([new UsagePriceTier { UpTo = null }, new UsagePriceTier { UpTo = 100m }], new PassThroughStringLocalizer<UsageTests>()));
        Assert.Empty(UsagePricing.Validate(_tiers, new PassThroughStringLocalizer<UsageTests>()));
    }

    [Fact]
    public async Task ARecurringPlan_BillsTheUsageOfThePeriodThatJustEnded_InTierBands()
    {
        // Arrange: a monthly plan from November 13 with a $50 base fee and graduated API calls; 7,000 calls between
        // November 13 and December 12, and 900 on December 13 (the next period).
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var meter = await CreateMeterAsync(harness, "API calls", "api-calls", UsageAggregation.Sum, unit: "calls");
        var plan = await CreatePlanAsync(harness, meter.ItemId, withBaseFee: true);
        await RecordAsync(harness, "api-calls", _customer, 4000m, "n1", new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "api-calls", _customer, 3000m, "n2", new DateTime(2026, 12, 12, 23, 0, 0, DateTimeKind.Utc));
        await RecordAsync(harness, "api-calls", _customer, 900m, "d1", new DateTime(2026, 12, 13, 1, 0, 0, DateTimeKind.Utc));

        // Act
        await RunAsync(harness, new DateOnly(2026, 11, 13));
        await RunAsync(harness, new DateOnly(2026, 12, 13));

        // Assert: the first run has no period behind it; the second bills three bands for November 13 to December 12.
        var runs = (await FindPlanAsync(harness, plan.ItemId)).Runs;
        Assert.Equal(2, runs.Count);
        var first = await harness.FindInvoiceAsync(runs[0].InvoiceId);
        var second = await harness.FindInvoiceAsync(runs[1].InvoiceId);
        Assert.Equal(["Base fee"], first.Lines.Select(line => line.Name));

        var usage = second.Lines.Where(line => line.Kind == DocumentLineKind.Usage).ToList();
        Assert.Equal([(1000m, 0.10m), (4000m, 0.05m), (2000m, 0.01m)], usage.Select(line => (line.Quantity, line.UnitPrice)));
        Assert.All(usage, line => Assert.Equal(("calls", new DateOnly(2026, 11, 13), new DateOnly(2026, 12, 12), meter.ItemId), (line.Unit, line.ServiceStart.Value, line.ServiceEnd.Value, line.SourceId)));
        Assert.StartsWith("API calls", usage[0].Name, StringComparison.Ordinal);
        Assert.Equal(370m, second.Totals.Total);
    }

    [Fact]
    public async Task AUsageOnlyPlan_MakesNoInvoice_ForAPeriodWithoutUsage()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var meter = await CreateMeterAsync(harness, "Storage", "storage", UsageAggregation.Maximum, unit: "GB");
        var plan = await CreatePlanAsync(harness, meter.ItemId, withBaseFee: false);

        // Act
        await RunAsync(harness, new DateOnly(2026, 11, 13));
        await RunAsync(harness, new DateOnly(2026, 12, 13));

        // Assert
        var saved = await FindPlanAsync(harness, plan.ItemId);
        Assert.Equal([RecurringRunOutcome.Skipped, RecurringRunOutcome.Skipped], saved.Runs.Select(run => run.Outcome));
        Assert.All(saved.Runs, run => Assert.Null(run.InvoiceId));
        Assert.Equal(new DateOnly(2027, 1, 13), saved.NextRunDate);
    }

    [Fact]
    public async Task APlan_WithTiersOutOfOrder_OrNoMeter_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var errors = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var plan = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            plan.Name = "Bad";
            plan.Rule = "FREQ=MONTHLY";
            plan.StartDate = new DateOnly(2026, 11, 13);
            plan.UsageCharges.Add(new RecurringUsageCharge { MeterId = null, Tiers = [.. _tiers] });
            plan.UsageCharges.Add(new RecurringUsageCharge { MeterId = "m", Tiers = [new() { UpTo = 500m }, new() { UpTo = 100m }] });

            return (await manager.ValidateAsync(plan, TestContext.Current.CancellationToken)).Errors.Select(error => error.ErrorMessage).ToList();
        });

        // Assert
        Assert.Contains(errors, error => error.Contains("meter", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("upper limit", StringComparison.OrdinalIgnoreCase));
    }

    private static Task<UsageMeter> CreateMeterAsync(InvoicingTestHarness harness, string name, string code, UsageAggregation aggregation, bool enabled = true, string unit = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IUsageMeterManager>();
            var meter = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            meter.Name = name;
            meter.Code = code;
            meter.Unit = unit;
            meter.Aggregation = aggregation;
            meter.Enabled = enabled;
            var validation = await manager.ValidateAsync(meter, TestContext.Current.CancellationToken);
            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            await manager.CreateAsync(meter, TestContext.Current.CancellationToken);

            return meter;
        });

    private static Task<UsageRecordResult> RecordAsync(InvoicingTestHarness harness, string meter, PartyRef party, decimal quantity, string eventId, DateTime? occurredUtc = null)
        => harness.InScopeAsync(services => services.GetRequiredService<IUsageService>().RecordAsync(new UsageRecordRequest
        {
            Meter = meter,
            Party = party,
            Quantity = quantity,
            EventId = eventId,
            OccurredUtc = occurredUtc,
        }, TestContext.Current.CancellationToken));

    private static Task<decimal> TotalAsync(InvoicingTestHarness harness, string code, PartyRef party, DateTime fromUtc, DateTime toUtc)
        => harness.InScopeAsync(async services =>
        {
            var meter = await services.GetRequiredService<IUsageMeterManager>().FindByCodeAsync(code, TestContext.Current.CancellationToken);

            return await services.GetRequiredService<IUsageService>().GetTotalAsync(meter.ItemId, party, fromUtc, toUtc, TestContext.Current.CancellationToken);
        });

    private static Task<RecurringPlan> CreatePlanAsync(InvoicingTestHarness harness, string meterId, bool withBaseFee)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var plan = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            plan.Name = "API plan";
            plan.Rule = "FREQ=MONTHLY;BYMONTHDAY=13";
            plan.StartDate = new DateOnly(2026, 11, 13);
            plan.Mode = RecurringPlanMode.Draft;
            plan.Currency = "USD";
            plan.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };

            if (withBaseFee)
            {
                plan.Lines.Add(new DocumentLine { Id = "line-1", Name = "Base fee", Quantity = 1m, UnitPrice = 50m });
            }

            plan.UsageCharges.Add(new RecurringUsageCharge { MeterId = meterId, TierMode = UsageTierMode.Graduated, Tiers = [.. _tiers] });
            await manager.CreateAsync(plan, TestContext.Current.CancellationToken);
            await services.GetRequiredService<IRecurringPlanService>().ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

            return plan;
        });

    private static Task<RecurringPlanResult> RunAsync(InvoicingTestHarness harness, DateOnly today)
        => harness.InScopeAsync(async services =>
        {
            var plans = await services.GetRequiredService<IRecurringPlanStore>().ListDueAsync(today, 10, TestContext.Current.CancellationToken);

            return await services.GetRequiredService<IRecurringPlanService>().RunAsync(plans.Single().ItemId, today, TestContext.Current.CancellationToken);
        });

    private static Task<RecurringPlan> FindPlanAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());
}
