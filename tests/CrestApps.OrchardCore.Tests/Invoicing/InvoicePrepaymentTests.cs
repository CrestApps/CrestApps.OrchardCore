using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A deposit is invoiced before the work as a prepayment invoice linked to the draft; once paid, the final invoice
/// deducts what was paid, once, as a negative deposit line; an estimate's paid deposit is deducted when the work is
/// invoiced; an unpaid deposit is never deducted.
/// </summary>
public sealed class InvoicePrepaymentTests
{
    private static readonly PartyRef _customer = new(PartyKinds.User, "user-1");

    [Fact]
    public async Task ADeposit_IsAskedAsALinkedPrepaymentDraft()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(BillToCustomer);

        // Act
        var deposit = await ServiceAsync(harness, service => service.RequestDepositAsync(draft.ItemId, 30m, null, TestContext.Current.CancellationToken));
        var tooMuch = await ServiceAsync(harness, service => service.RequestDepositAsync(draft.ItemId, null, 200m, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(deposit.Succeeded, string.Join(" ", deposit.Errors));
        var prepayment = await harness.FindInvoiceAsync(deposit.Invoice.ItemId);
        Assert.Equal((InvoiceKind.Prepayment, InvoiceStatus.Draft, draft.ItemId, 30m), (prepayment.Kind, prepayment.Status, prepayment.RelatedInvoiceId, prepayment.Totals.Total));
        Assert.Equal((DocumentLineKind.Deposit, TaxTreatment.OutOfScope), (prepayment.Lines[0].Kind, prepayment.Lines[0].TaxTreatment));
        Assert.Equal(_customer, prepayment.BillTo.Party);
        Assert.False(tooMuch.Succeeded);
    }

    [Fact]
    public async Task APaidDeposit_IsDeductedOnce_FromTheFinalInvoice()
    {
        // Arrange: a $30 deposit on a $100 invoice, issued and paid.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(BillToCustomer);
        var prepayment = await IssueAndPayDepositAsync(harness, draft.ItemId, 30m);
        var other = await harness.CreateDraftAsync(BillToCustomer);

        // Act
        var before = await DeductibleAsync(harness, draft.ItemId);
        var deducted = await ServiceAsync(harness, service => service.DeductAsync(draft.ItemId, prepayment.ItemId, TestContext.Current.CancellationToken));
        var again = await ServiceAsync(harness, service => service.DeductAsync(draft.ItemId, prepayment.ItemId, TestContext.Current.CancellationToken));
        var after = await DeductibleAsync(harness, draft.ItemId);
        var elsewhere = await DeductibleAsync(harness, other.ItemId);

        // Assert
        Assert.Equal([prepayment.ItemId], before.Select(invoice => invoice.ItemId));
        Assert.True(deducted.Succeeded, string.Join(" ", deducted.Errors));
        Assert.False(again.Succeeded);
        Assert.Empty(after);
        Assert.Empty(elsewhere);

        var final = await harness.FindInvoiceAsync(draft.ItemId);
        var line = final.Lines.Single(item => item.Kind == DocumentLineKind.Deposit);
        Assert.Equal((-30m, 70m), (line.UnitPrice, final.Totals.Total));
        Assert.Contains(prepayment.Number, line.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnpaidDeposit_IsNotDeductible()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(BillToCustomer);
        var deposit = await ServiceAsync(harness, service => service.RequestDepositAsync(draft.ItemId, null, 40m, TestContext.Current.CancellationToken));
        var issued = await harness.RunAsync(commands => commands.IssueAsync(deposit.Invoice.ItemId, TestContext.Current.CancellationToken));

        // Act
        var deductible = await DeductibleAsync(harness, draft.ItemId);

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Empty(deductible);
    }

    [Fact]
    public async Task TheEstimatesPaidDeposit_IsDeducted_WhenTheWorkIsInvoiced()
    {
        // Arrange: 30% of a $100 estimate invoiced as a deposit, issued and paid.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IEstimateManager>();
            var draft = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            draft.Currency = "USD";
            draft.DepositPercent = 30m;
            draft.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            draft.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m });
            await manager.CreateAsync(draft, TestContext.Current.CancellationToken);

            return draft;
        });

        var sent = await EstimateAsync(harness, commands => commands.SendAsync(estimate.ItemId, TestContext.Current.CancellationToken));
        var deposit = await EstimateAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Deposit, TestContext.Current.CancellationToken));
        var paid = await IssueAndPayAsync(harness, deposit.Invoice.ItemId, 30m);

        // Act
        var work = await EstimateAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Full, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(sent.Succeeded && work.Succeeded, string.Join(" ", work.Errors));
        var final = await harness.FindInvoiceAsync(work.Invoice.ItemId);
        Assert.Equal(70m, final.Totals.Total);
        Assert.Equal(paid.ItemId, final.Lines.Single(line => line.Kind == DocumentLineKind.Deposit).SourceId);
    }

    private static void BillToCustomer(Invoice invoice)
        => invoice.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };

    private static async Task<Invoice> IssueAndPayDepositAsync(InvoicingTestHarness harness, string draftId, decimal amount)
    {
        var deposit = await ServiceAsync(harness, service => service.RequestDepositAsync(draftId, null, amount, TestContext.Current.CancellationToken));

        Assert.True(deposit.Succeeded, string.Join(" ", deposit.Errors));

        return await IssueAndPayAsync(harness, deposit.Invoice.ItemId, amount);
    }

    private static async Task<Invoice> IssueAndPayAsync(InvoicingTestHarness harness, string invoiceId, decimal amount)
    {
        var issued = await harness.RunAsync(commands => commands.IssueAsync(invoiceId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(issued.Invoice.TransactionId, amount, "check", null, TestContext.Current.CancellationToken));

        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));

        return await harness.FindInvoiceAsync(invoiceId);
    }

    private static Task<IReadOnlyList<Invoice>> DeductibleAsync(InvoicingTestHarness harness, string invoiceId)
        => harness.InScopeAsync(services => services.GetRequiredService<IInvoicePrepaymentService>().ListDeductibleAsync(invoiceId, TestContext.Current.CancellationToken));

    private static Task<InvoiceCommandResult> ServiceAsync(InvoicingTestHarness harness, Func<IInvoicePrepaymentService, Task<InvoiceCommandResult>> command)
        => harness.InScopeAsync(services => command(services.GetRequiredService<IInvoicePrepaymentService>()));

    private static Task<EstimateCommandResult> EstimateAsync(InvoicingTestHarness harness, Func<IEstimateCommandService, Task<EstimateCommandResult>> command)
        => harness.InScopeAsync(services => command(services.GetRequiredService<IEstimateCommandService>()));
}
