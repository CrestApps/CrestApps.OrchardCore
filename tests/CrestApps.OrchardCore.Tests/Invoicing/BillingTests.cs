using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Billing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Work and costs kept to bill later go on a draft invoice at their marked-up price, are billed once, stay billed while
/// the invoice is, and are let go when their line is taken off, the draft deleted or the invoice voided; a recurring plan
/// can bill them on its runs; and ended Time Tracker time is billed at an hourly rate, recorded on the entry.
/// </summary>
public sealed class BillingTests
{
    private static readonly PartyRef _customer = new(PartyKinds.User, "user-1");

    [Fact]
    public async Task ACharge_IsAddedAtItsMarkedUpPrice_BilledOnce_AndLetGoWhenTheInvoiceIsVoided()
    {
        // Arrange: a $200 expense passed on at cost plus 10%.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var charge = await AddChargeAsync(harness, "Travel to the site", 200m, 10m);
        var draft = await harness.CreateDraftAsync(BillToCustomer);

        // Act
        var before = await UnbilledAsync(harness);
        var added = await BillingAsync(harness, billing => billing.AddToInvoiceAsync(draft.ItemId, ChargesBillableSource.SourceType, [charge.ItemId], new BillableQuery(), TestContext.Current.CancellationToken));
        var afterAdding = await UnbilledAsync(harness);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var stillBilled = await FindChargeAsync(harness, charge.ItemId);
        var voided = await harness.RunAsync(commands => commands.VoidAsync(draft.ItemId, "Wrong customer", TestContext.Current.CancellationToken));
        var afterVoid = await UnbilledAsync(harness);

        // Assert
        Assert.Single(before);
        Assert.True(added.Succeeded, string.Join(" ", added.Errors));
        Assert.Empty(afterAdding);
        Assert.True(issued.Succeeded && voided.Succeeded, string.Join(" ", issued.Errors.Concat(voided.Errors)));
        Assert.Equal(draft.ItemId, stillBilled.InvoiceId);
        Assert.Equal([charge.ItemId], afterVoid.Select(candidate => candidate.Id));

        var line = issued.Invoice.Lines.Single(item => item.SourceId == charge.ItemId);
        Assert.Equal((DocumentLineKind.Expense, 220m, "billable:charges"), (line.Kind, line.UnitPrice, line.SourceType));
    }

    [Fact]
    public async Task TakingTheLineOff_OrDeletingTheDraft_LetsTheChargeGo()
    {
        // Arrange: two charges on one draft.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var first = await AddChargeAsync(harness, "Parts", 40m, 0m);
        var second = await AddChargeAsync(harness, "Disposal fee", 15m, 0m);
        var draft = await harness.CreateDraftAsync(BillToCustomer);
        await BillingAsync(harness, billing => billing.AddToInvoiceAsync(draft.ItemId, ChargesBillableSource.SourceType, [first.ItemId, second.ItemId], new BillableQuery(), TestContext.Current.CancellationToken));

        // Act: the first line taken off and the draft saved, then the draft deleted.
        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IInvoiceManager>();
            var invoice = await manager.FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken);

            invoice.Lines.Remove(invoice.Lines.Single(line => line.SourceId == first.ItemId));
            await manager.UpdateAsync(invoice, cancellationToken: TestContext.Current.CancellationToken);

            return true;
        });
        var afterRemoving = await UnbilledAsync(harness);

        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IInvoiceManager>();
            await manager.DeleteAsync(await manager.FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

            return true;
        });
        var afterDeleting = await UnbilledAsync(harness);

        // Assert
        Assert.Equal([first.ItemId], afterRemoving.Select(candidate => candidate.Id));
        Assert.Equal(new[] { first.ItemId, second.ItemId }.Order(StringComparer.Ordinal), afterDeleting.Select(candidate => candidate.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ARecurringPlan_CanBillTheUnbilledCharges_UpToItsRun()
    {
        // Arrange: a charge on November 3 and one after the run's day.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var due = await AddChargeAsync(harness, "Extra support hour", 90m, 0m, new DateOnly(2026, 11, 3));
        var later = await AddChargeAsync(harness, "Next month's visit", 50m, 0m, new DateOnly(2026, 12, 20));
        var plan = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var draft = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            draft.Name = "Support";
            draft.Rule = "FREQ=MONTHLY;BYMONTHDAY=13";
            draft.StartDate = new DateOnly(2026, 11, 13);
            draft.Mode = RecurringPlanMode.Draft;
            draft.Currency = "USD";
            draft.BillUnbilledCharges = true;
            draft.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            draft.Lines.Add(new DocumentLine { Id = "line-1", Name = "Support plan", Quantity = 1m, UnitPrice = 300m });
            await manager.CreateAsync(draft, TestContext.Current.CancellationToken);

            return draft;
        });
        await harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanService>().ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken));

        // Act
        var made = await harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanService>().RunDueAsync(new DateOnly(2026, 11, 13), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, made);
        var invoiceId = (await harness.InScopeAsync(services => services.GetRequiredService<IRecurringPlanStore>().FindByIdAsync(plan.ItemId, TestContext.Current.CancellationToken).AsTask())).Runs[0].InvoiceId;
        var invoice = await harness.FindInvoiceAsync(invoiceId);
        Assert.Equal(["Support plan", "Extra support hour"], invoice.Lines.Select(line => line.Name));
        Assert.Equal(invoiceId, (await FindChargeAsync(harness, due.ItemId)).InvoiceId);
        Assert.Null((await FindChargeAsync(harness, later.ItemId)).InvoiceId);
    }

    [Fact]
    public async Task EndedTime_IsOfferedAtTheHourlyRate_AndTheEntryRecordsTheLineThatBillsIt()
    {
        // Arrange: 90 minutes ended, 30 minutes running, and an hour billed already.
        var start = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);
        var tracker = new Mock<ITimeTrackerService>();
        tracker.Setup(service => service.ListAsync(It.IsAny<TimeEntryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new TimeEntry { ItemId = "t1", UserId = "user-9", UserName = "Pat", StageId = "s1", StartUtc = start, EndUtc = start.AddMinutes(90) },
            new TimeEntry { ItemId = "t2", UserId = "user-9", UserName = "Pat", StageId = "s1", StartUtc = start.AddHours(3) },
            new TimeEntry { ItemId = "t3", UserId = "user-9", UserName = "Pat", StageId = "s1", StartUtc = start.AddDays(-1), EndUtc = start.AddDays(-1).AddHours(1), BilledInvoiceId = "other" },
        ]);
        tracker.Setup(service => service.SetBilledAsync("t1", "invoice-1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TimeTrackerResult());
        var stages = new Mock<ITimeStageManager>();
        stages.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new TimeStage { ItemId = "s1", Name = "Working" }]);
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>(zone => zone.TimeZoneId == "UTC"));
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset utc) => utc);

        var source = new TimeTrackerBillableSource(tracker.Object, stages.Object, localClock.Object, new PassThroughStringLocalizer<TimeTrackerBillableSource>());

        // Act
        var candidates = await source.ListUnbilledAsync(new BillableQuery { UserId = "user-9", HourlyRate = 120m, Currency = "USD" }, TestContext.Current.CancellationToken);
        var refused = await source.MarkBilledAsync("t1", "invoice-1", "line-1", TestContext.Current.CancellationToken);

        // Assert
        var candidate = Assert.Single(candidates);
        Assert.Equal(("t1", DocumentLineKind.Time, 1.5m, 120m), (candidate.Id, candidate.Kind, candidate.Quantity, candidate.UnitPrice));
        Assert.Contains("Working", candidate.Description, StringComparison.Ordinal);
        Assert.Null(refused);
        tracker.Verify(service => service.SetBilledAsync("t1", "invoice-1", "line-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void BillToCustomer(Invoice invoice)
        => invoice.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };

    private static Task<IReadOnlyList<BillableCandidate>> UnbilledAsync(InvoicingTestHarness harness)
        => harness.InScopeAsync(services => services.GetRequiredService<IBillingService>().ListUnbilledAsync(ChargesBillableSource.SourceType, new BillableQuery { Party = _customer, Currency = "USD" }, TestContext.Current.CancellationToken));

    private static Task<T> BillingAsync<T>(InvoicingTestHarness harness, Func<IBillingService, Task<T>> call)
        => harness.InScopeAsync(services => call(services.GetRequiredService<IBillingService>()));

    private static Task<BillableItem> FindChargeAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IBillableItemManager>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private static Task<BillableItem> AddChargeAsync(InvoicingTestHarness harness, string description, decimal cost, decimal markup, DateOnly? date = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IBillableItemManager>();
            var item = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            item.Party = _customer;
            item.PartyName = "Fabrikam Ltd.";
            item.Description = description;
            item.Date = date ?? new DateOnly(2026, 11, 5);
            item.UnitCost = cost;
            item.MarkupPercent = markup;
            item.Currency = "USD";

            var validation = await manager.ValidateAsync(item, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(item, TestContext.Current.CancellationToken);

            return item;
        });
}
