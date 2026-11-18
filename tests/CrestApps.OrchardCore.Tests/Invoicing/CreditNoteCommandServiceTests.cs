using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A credit note credits what is still creditable on an issued invoice, and issuing it lowers the balance, or keeps the
/// amount as the customer's credit, once.
/// </summary>
public sealed class CreditNoteCommandServiceTests
{
    [Fact]
    public async Task ADraft_CreditsEachLineOfTheInvoice_AndASecondRequestOpensTheSameDraft()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var first = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));
        var second = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        var line = Assert.Single(first.CreditNote.Lines);
        Assert.Equal(("line-1", 100m), (line.CreditedLineId, first.CreditNote.Totals.Total));
        Assert.Equal((invoice.ItemId, invoice.Number, CreditNoteDisposition.ReduceBalance), (first.CreditNote.InvoiceId, first.CreditNote.InvoiceNumber, first.CreditNote.Disposition));
        Assert.True(second.AlreadyDone);
        Assert.Equal(first.CreditNote.ItemId, second.CreditNote.ItemId);
    }

    [Fact]
    public async Task Issuing_OnAnOpenInvoice_LowersWhatIsOwed_AndSettlesIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var draft = await DraftAsync(harness, invoice);

        // Act
        var issued = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var repeated = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.True(repeated.AlreadyDone);
        Assert.False(string.IsNullOrEmpty(issued.CreditNote.Number));

        var transaction = await harness.FindTransactionAsync(invoice.TransactionId);
        var credit = Assert.Single(transaction.Events, entry => entry.Type == TransactionEventType.Adjusted);
        Assert.Equal((TransactionAdjustmentKind.Credit, 100m, InvoicingConstants.CreditNoteDocumentKind, draft.ItemId), (credit.AdjustmentKind.Value, credit.Amount.Value, credit.DocumentKind, credit.DocumentId));
        Assert.Equal(0m, transaction.OutstandingAmount);

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((100m, 0m, InvoiceSettlement.Paid), (stored.AmountCredited, stored.AmountDue, stored.Settlement));
        Assert.Contains(stored.History, entry => entry.Kind == DocumentEventKinds.Credited && entry.ReferenceId == draft.ItemId);
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(invoice.ItemId));
    }

    [Fact]
    public async Task LoweringTheBalanceByMoreThanIsOwed_IsRefused_AndTakesNoNumber()
    {
        // Arrange: 60 of the 100 paid, so only 40 is owed.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var draft = await DraftAsync(harness, invoice);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 60m }, TestContext.Current.CancellationToken));
        var numbers = harness.Numbers.Issued;

        // Act
        var issued = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(issued.Succeeded);
        Assert.Equal(numbers, harness.Numbers.Issued);
        Assert.Equal(40m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
        Assert.Equal(CreditNoteStatus.Draft, (await FindCreditNoteAsync(harness, draft.ItemId)).Status);
    }

    [Fact]
    public async Task Issuing_AsCustomerCredit_KeepsTheAmountForThePayerOnce()
    {
        // Arrange: a paid invoice billed to a user who can hold credit.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var payer = new PartyRef(PartyKinds.User, "user-1");
        harness.Credit.Setup(credit => credit.CanHold(It.IsAny<PartyRef>())).Returns(true);
        harness.Credit
            .Setup(credit => credit.AddAsync(It.IsAny<CustomerCreditChange>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerCreditResult { Succeeded = true, Balance = 100m });

        var invoice = await IssueAsync(harness, draft => draft.BillTo.Party = payer);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken));
        var draft = await DraftAsync(harness, invoice);

        // Act
        var issued = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(CreditNoteDisposition.CustomerCredit, draft.Disposition);
        harness.Credit.Verify(credit => credit.AddAsync(
            It.Is<CustomerCreditChange>(change => change.Party == payer && change.Amount == 100m && change.Currency == "USD" && change.Reference == DefaultCreditNoteCommandService.GetCreditReference(draft.ItemId)),
            It.IsAny<CancellationToken>()), Times.Once);

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((100m, 100m, InvoiceSettlement.Paid), (stored.AmountPaid, stored.AmountCredited, stored.Settlement));
    }

    [Fact]
    public async Task AFullyCreditedInvoice_CantBeCreditedAgain()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var draft = await DraftAsync(harness, invoice);
        await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Act
        var again = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(again.Succeeded);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, Action<Invoice> configure = null)
    {
        var draft = await harness.CreateDraftAsync(configure);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static async Task<CreditNote> DraftAsync(InvoicingTestHarness harness, Invoice invoice)
    {
        var drafted = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));

        Assert.True(drafted.Succeeded, string.Join(" ", drafted.Errors));

        return drafted.CreditNote;
    }

    private static Task<CreditNote> FindCreditNoteAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());
}
