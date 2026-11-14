using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoice commands against a real store and ledger: issue numbers the invoice and posts one transaction, void and
/// write-off decide from the transaction (never from the amounts mirrored on the invoice), and each command repeated
/// after it completed changes nothing.
/// </summary>
public sealed class InvoiceCommandServiceTests
{
    [Fact]
    public async Task Issue_NumbersTheInvoice_PostsOneTransaction_AndOpensIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var repeated = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.True(repeated.AlreadyDone);
        Assert.Equal(1, harness.Numbers.Issued);

        var invoice = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal("INV-1001", invoice.Number);
        Assert.Equal(100m, invoice.Totals.Total);
        Assert.Equal(100m, invoice.AmountDue);
        Assert.Equal(new DateOnly(2026, 11, 13), invoice.IssueDate);
        Assert.Equal(invoice.IssueDate, invoice.DueDate);
        Assert.Equal("Contoso Supplies", invoice.Issuer.Name);
        Assert.False(string.IsNullOrEmpty(invoice.ContentHash));
        Assert.Contains(invoice.History, entry => entry.Kind == DocumentEventKinds.Issued);

        var transaction = await harness.FindTransactionAsync(invoice.TransactionId);
        Assert.Equal((InvoicingConstants.InvoiceReferenceType, invoice.ItemId), (transaction.ReferenceType, transaction.ReferenceId));
        Assert.Equal(("INV-1001", 100m, TransactionStatus.Outstanding), (transaction.InvoiceNumber, transaction.TotalAmount, transaction.Status));
        Assert.Equal((CustomerOwnerKind.Guest, invoice.ItemId), (transaction.OwnerKind, transaction.OwnerId));
        Assert.Equal("ap@fabrikam.example", transaction.GuestContactEmail);
    }

    [Fact]
    public async Task Issue_ToAUser_FillsTheirNameAndEmail_AndTheyOwnTheTransaction()
    {
        // Arrange: no billing profile source knows the user; the user store does.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        harness.Contacts
            .Setup(resolver => resolver.ResolveAsync(It.Is<CustomerOwner>(owner => owner.Id == "user-7"), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerContact { DisplayName = "Pat Doe", Email = "pat@example.com" });

        var draft = await harness.CreateDraftAsync(invoice => invoice.BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.User, "user-7") });

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(("Pat Doe", "pat@example.com"), (issued.Invoice.BillTo.Contact.Name, issued.Invoice.BillTo.Contact.Email));

        var transaction = await harness.FindTransactionAsync(issued.Invoice.TransactionId);
        Assert.Equal((CustomerOwnerKind.Authenticated, "user-7"), (transaction.OwnerKind, transaction.OwnerId));
        Assert.Equal(new PartyRef(PartyKinds.User, "user-7"), transaction.GetPayer());
    }

    [Fact]
    public async Task Issue_WithABadDiscount_IsRefused_AndTakesNoNumber()
    {
        // Arrange: saved before the rule existed, or changed behind the editor.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        await harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IInvoiceStore>();
            var stored = await store.FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken);
            stored.Lines[0].DiscountPercent = 150m;
            await store.UpdateAsync(stored, TestContext.Current.CancellationToken);

            return stored;
        });

        // Act
        var result = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0, harness.Numbers.Issued);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(draft.ItemId)).Status);
    }

    [Fact]
    public async Task Issue_AZeroTotal_IsPaidAtOnce_AndPostsNothing()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(invoice => invoice.Lines[0].UnitPrice = 0m);

        // Act
        var result = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(InvoiceSettlement.Paid, result.Invoice.Settlement);
        Assert.Null(result.Invoice.TransactionId);
        Assert.Equal("INV-1001", result.Invoice.Number);
    }

    [Fact]
    public async Task Void_CancelsTheTransaction_KeepsTheNumber_AndRepeatsSafely()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var voided = await harness.RunAsync(commands => commands.VoidAsync(invoice.ItemId, "Billed the wrong customer", TestContext.Current.CancellationToken));
        var repeated = await harness.RunAsync(commands => commands.VoidAsync(invoice.ItemId, "Billed the wrong customer", TestContext.Current.CancellationToken));

        // Assert
        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));
        Assert.True(repeated.AlreadyDone);

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((InvoiceStatus.Void, "INV-1001", 0m), (stored.Status, stored.Number, stored.AmountDue));
        Assert.Equal(TransactionStatus.Canceled, (await harness.FindTransactionAsync(invoice.TransactionId)).Status);
    }

    /// <summary>
    /// A payment recorded a moment before a void is in the ledger but not yet mirrored on the invoice: the void is decided
    /// by the ledger under the transaction's lock, so it is refused and the payment is never canceled.
    /// </summary>
    [Fact]
    public async Task Void_RightAfterAPaymentTheInvoiceDoesNotShowYet_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 10m }, TestContext.Current.CancellationToken));
        Assert.True(paid.Succeeded);
        Assert.Equal(0m, (await harness.FindInvoiceAsync(invoice.ItemId)).AmountPaid);

        // Act
        var voided = await harness.RunAsync(commands => commands.VoidAsync(invoice.ItemId, "Duplicate", TestContext.Current.CancellationToken));

        // Assert
        Assert.False(voided.Succeeded);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);

        var transaction = await harness.FindTransactionAsync(invoice.TransactionId);
        Assert.Equal((TransactionStatus.PartiallyPaid, 10m), (transaction.Status, transaction.AmountPaid));
    }

    [Fact]
    public async Task Void_WithAnIssuedCreditNote_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        await harness.InScopeAsync(async services =>
        {
            var notes = services.GetRequiredService<ICreditNoteStore>();
            var note = new CreditNote { ItemId = "cn-1", InvoiceId = invoice.ItemId, InvoiceNumber = invoice.Number, Number = "CN-1001", Status = CreditNoteStatus.Issued, Currency = "USD" };
            await notes.CreateAsync(note, TestContext.Current.CancellationToken);

            return note;
        });

        // Act
        var voided = await harness.RunAsync(commands => commands.VoidAsync(invoice.ItemId, "Duplicate", TestContext.Current.CancellationToken));

        // Assert
        Assert.False(voided.Succeeded);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
        Assert.Equal(TransactionStatus.Outstanding, (await harness.FindTransactionAsync(invoice.TransactionId)).Status);
    }

    [Fact]
    public async Task WriteOff_TakesWhatTheLedgerStillOwes_AndReinstateTakesItBack()
    {
        // Arrange: 30 paid in the ledger, not yet mirrored on the invoice.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 30m }, TestContext.Current.CancellationToken));

        // Act
        var writtenOff = await harness.RunAsync(commands => commands.WriteOffAsync(invoice.ItemId, "Customer went out of business", TestContext.Current.CancellationToken));
        var transactionAfterWriteOff = await harness.FindTransactionAsync(invoice.TransactionId);
        var reinstated = await harness.RunAsync(commands => commands.ReinstateAsync(invoice.ItemId, "They paid after all", TestContext.Current.CancellationToken));

        // Assert
        Assert.True(writtenOff.Succeeded, string.Join(" ", writtenOff.Errors));
        Assert.Equal(70m, Assert.Single(transactionAfterWriteOff.Events, entry => entry.Type == TransactionEventType.Adjusted).Amount);
        Assert.Equal(0m, transactionAfterWriteOff.OutstandingAmount);

        Assert.True(reinstated.Succeeded, string.Join(" ", reinstated.Errors));

        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((InvoiceStatus.Open, 70m), (stored.Status, stored.AmountDue));
        Assert.Equal(70m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task AnIssuedInvoice_CantBeChangedThroughTheManager()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var validation = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<CrestApps.OrchardCore.Invoicing.Services.IInvoiceManager>();
            var stored = await manager.FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken);
            stored.Lines[0].UnitPrice = 1m;

            return await manager.ValidateAsync(stored, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.False(validation.Succeeded);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
