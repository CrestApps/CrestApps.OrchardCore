using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A recurring plan makes one invoice per occurrence of its schedule, with the period written into its lines, catches up
/// runs it missed (within a limit), never makes a second invoice for an occurrence, skips what it missed while paused,
/// raises its prices on schedule, ends after its runs, and keeps a failed run's draft.
/// </summary>
public sealed class RecurringPlanTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    [Fact]
    public async Task ADueRun_IssuesTheInvoice_WithItsPeriod_AndMovesToTheNextOccurrence()
    {
        // Arrange: monthly on the 1st, from November 1st.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateActiveAsync(harness, new DateOnly(2026, 11, 1));

        // Act
        var made = await RunDueAsync(harness);
        var again = await RunDueAsync(harness);

        // Assert
        Assert.Equal((1, 0), (made, again));
        var stored = await FindAsync(harness, plan.ItemId);
        Assert.Equal((new DateOnly(2026, 12, 1), 1, RecurringPlanStatus.Active), (stored.NextRunDate, stored.RunCount, stored.Status));

        var run = Assert.Single(stored.Runs);
        Assert.Equal((RecurringRunOutcome.Issued, new DateOnly(2026, 11, 1)), (run.Outcome, run.Occurrence));

        var invoice = await harness.FindInvoiceAsync(run.InvoiceId);
        Assert.Equal((InvoiceStatus.Open, InvoicingConstants.RecurringPlanReferenceType, $"{plan.ItemId}:2026-11-01"), (invoice.Status, invoice.SourceType, invoice.SourceId));
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), (invoice.ServiceStart, invoice.ServiceEnd));
        Assert.Equal("Hosting November 2026 (Nov 1 to Nov 30)", Assert.Single(invoice.Lines).Name);
        Assert.Contains("RecurringGenerated:Hosting:2026-11-01", harness.Observed);
    }

    [Fact]
    public async Task AnOccurrenceWithAnInvoice_IsSkipped_NotInvoicedTwice()
    {
        // Arrange: a run made, then the plan put back on the same occurrence.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateActiveAsync(harness, new DateOnly(2026, 11, 1));
        await RunDueAsync(harness);

        await harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IRecurringPlanStore>();
            var stored = await store.FindByIdAsync(plan.ItemId, TestContext.Current.CancellationToken);
            stored.NextRunDate = new DateOnly(2026, 11, 1);
            await store.UpdateAsync(stored, TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        var made = await RunDueAsync(harness);

        // Assert
        Assert.Equal(0, made);
        var runs = (await FindAsync(harness, plan.ItemId)).Runs;
        Assert.Equal([RecurringRunOutcome.Issued, RecurringRunOutcome.Skipped], runs.Select(run => run.Outcome));
        Assert.Equal(runs[0].InvoiceId, runs[1].InvoiceId);
    }

    [Fact]
    public async Task MissedRuns_AreCaughtUp_AndThePlanEndsAfterItsRuns_WithItsPricesRaised()
    {
        // Arrange: from August, three runs, prices up 10% every 2 runs, left as drafts.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateActiveAsync(harness, new DateOnly(2026, 8, 1), configure: draft =>
        {
            draft.MaxRuns = 3;
            draft.Mode = RecurringPlanMode.Draft;
            draft.PriceIncreasePercent = 10m;
            draft.PriceIncreaseEveryRuns = 2;
        });

        // Act
        var made = await RunDueAsync(harness);

        // Assert
        Assert.Equal(3, made);
        var stored = await FindAsync(harness, plan.ItemId);
        Assert.Equal((RecurringPlanStatus.Completed, (DateOnly?)null), (stored.Status, stored.NextRunDate));

        var invoices = new List<Invoice>();

        foreach (var run in stored.Runs)
        {
            invoices.Add(await harness.FindInvoiceAsync(run.InvoiceId));
        }

        Assert.All(invoices, invoice => Assert.Equal(InvoiceStatus.Draft, invoice.Status));
        Assert.Equal([100m, 100m, 110m], invoices.Select(invoice => invoice.Lines[0].UnitPrice));
    }

    [Fact]
    public async Task RunsMissedWhilePaused_AreSkipped()
    {
        // Arrange: from September, paused before it ran.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateActiveAsync(harness, new DateOnly(2026, 9, 1));

        // Act
        var paused = await ServiceAsync(harness, service => service.PauseAsync(plan.ItemId, TestContext.Current.CancellationToken));
        var resumed = await ServiceAsync(harness, service => service.ResumeAsync(plan.ItemId, _today, TestContext.Current.CancellationToken));
        var made = await RunDueAsync(harness);

        // Assert
        Assert.True(paused.Succeeded && resumed.Succeeded);
        Assert.Equal((RecurringPlanStatus.Active, new DateOnly(2026, 12, 1)), (resumed.Plan.Status, resumed.Plan.NextRunDate));
        Assert.Equal(0, made);
    }

    [Fact]
    public async Task AFailedRun_KeepsItsDraft_IsRecorded_AndMovesOn()
    {
        // Arrange: the plan's customer was taken off after it started, so the invoice can't be issued.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateActiveAsync(harness, new DateOnly(2026, 11, 1));

        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var active = await manager.FindByIdAsync(plan.ItemId, TestContext.Current.CancellationToken);

            active.BillTo = new DocumentParty();
            await manager.UpdateAsync(active, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        var made = await RunDueAsync(harness);

        // Assert
        Assert.Equal(0, made);
        var stored = await FindAsync(harness, plan.ItemId);
        var run = Assert.Single(stored.Runs);
        Assert.Equal(RecurringRunOutcome.Failed, run.Outcome);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(run.InvoiceId)).Status);
        Assert.Equal(new DateOnly(2026, 12, 1), stored.NextRunDate);
        Assert.Contains("RecurringFailed:Hosting:2026-11-01", harness.Observed);
    }

    [Fact]
    public async Task TheUpcomingRuns_FollowTheRule_WithinTheEnd()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var plan = await CreateDraftAsync(harness, new DateOnly(2026, 11, 15), configure: draft =>
        {
            draft.Rule = "FREQ=WEEKLY;INTERVAL=2";
            draft.EndDate = new DateOnly(2026, 12, 31);
        });

        // Act
        var upcoming = await harness.InScopeAsync(services => Task.FromResult(services.GetRequiredService<IRecurringPlanService>().GetUpcoming(plan, 10)));

        // Assert
        Assert.Equal([new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 13), new DateOnly(2026, 12, 27)], upcoming);
    }

    [Theory]
    [InlineData("Hosting {{ period.month }} {{ period.year }}", "Hosting November 2026")]
    [InlineData("From {{period.start | date: \"yyyy-MM-dd\"}} to {{ period.end | date: 'yyyy-MM-dd' }}", "From 2026-11-01 to 2026-11-30")]
    [InlineData("No placeholders", "No placeholders")]
    [InlineData("{{ period.other }}", "{{ period.other }}")]
    public void ThePeriod_IsWrittenIntoText(string text, string expected)
    {
        // Act
        var rendered = DefaultRecurringPlanService.RenderPeriod(text, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        // Assert
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public async Task APlanWithNothingToBill_OrNoOneToBill_CannotBeStarted()
    {
        // Arrange: drafts saved half done.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var noLines = await CreateDraftAsync(harness, new DateOnly(2026, 12, 1), plan => plan.Lines.Clear());
        var noOne = await CreateDraftAsync(harness, new DateOnly(2026, 12, 1), plan => plan.BillTo = new DocumentParty());

        // Act
        var first = await ServiceAsync(harness, service => service.ActivateAsync(noLines.ItemId, TestContext.Current.CancellationToken));
        var second = await ServiceAsync(harness, service => service.ActivateAsync(noOne.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("at least one line", Assert.Single(first.Errors), StringComparison.Ordinal);
        Assert.Contains("who the plan bills", Assert.Single(second.Errors), StringComparison.Ordinal);
        Assert.Equal(RecurringPlanStatus.Draft, (await FindAsync(harness, noLines.ItemId)).Status);
    }

    private static Task<int> RunDueAsync(InvoicingTestHarness harness)
        => harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanService>().RunDueAsync(_today, TestContext.Current.CancellationToken));

    private static Task<RecurringPlanResult> ServiceAsync(InvoicingTestHarness harness, Func<IRecurringPlanService, Task<RecurringPlanResult>> command)
        => harness.InScopeAsync(services => command(services.GetRequiredService<IRecurringPlanService>()));

    private static Task<RecurringPlan> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private static async Task<RecurringPlan> CreateActiveAsync(InvoicingTestHarness harness, DateOnly start, Action<RecurringPlan> configure = null)
    {
        var plan = await CreateDraftAsync(harness, start, configure);
        var activated = await ServiceAsync(harness, service => service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken));

        Assert.True(activated.Succeeded, string.Join(" ", activated.Errors));

        return activated.Plan;
    }

    private static Task<RecurringPlan> CreateDraftAsync(InvoicingTestHarness harness, DateOnly start, Action<RecurringPlan> configure = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var plan = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            plan.Name = "Hosting";
            plan.Rule = "FREQ=MONTHLY;BYMONTHDAY=1";
            plan.StartDate = start;
            plan.Mode = RecurringPlanMode.Issue;
            plan.Currency = "USD";
            plan.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            plan.Lines.Add(new DocumentLine { Id = "line-1", Name = "Hosting {{ period.month }} {{ period.year }} ({{ period.start | date: \"MMM d\" }} to {{ period.end | date: \"MMM d\" }})", Quantity = 1m, UnitPrice = 100m });

            configure?.Invoke(plan);

            var validation = await manager.ValidateAsync(plan, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(plan, TestContext.Current.CancellationToken);

            return plan;
        });
}
