using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A customer's activity statement carries the balance from before its period, lists what was charged and paid in it
/// with the running balance, and ends on what is owed; an open-item statement lists the invoices still owed with how late
/// each is; each currency is a statement of its own, and void invoices are left out.
/// </summary>
public sealed class InvoiceStatementTests
{
    private static readonly PartyRef _customer = new(PartyKinds.User, "user-1");

    [Fact]
    public async Task AnActivityStatement_CarriesTheBalanceForward_AndRunsItThroughThePeriod()
    {
        // Arrange: $100 invoiced on October 1, $50 on November 5, and $40 paid on November 13.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var october = await IssueAsync(harness, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 100m);
        var november = await IssueAsync(harness, new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 5), 50m);
        await PayAsync(harness, october, 40m);
        var voided = await IssueAsync(harness, new DateOnly(2026, 11, 6), new DateOnly(2026, 12, 6), 70m);
        var voiding = await harness.RunAsync(commands => commands.VoidAsync(voided.ItemId, "Wrong customer", TestContext.Current.CancellationToken));

        // Act
        var statement = await BuildAsync(harness, new InvoiceStatementRequest
        {
            Party = _customer,
            Kind = InvoiceStatementKind.Activity,
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 30),
        });

        // Assert
        Assert.True(voiding.Succeeded, string.Join(" ", voiding.Errors));
        Assert.Equal(("USD", 100m, 110m), (statement.Currency, statement.OpeningBalance, statement.ClosingBalance));
        Assert.Equal(
            [(InvoiceStatementLineKind.Invoice, november.Number, 50m, 0m, 150m), (InvoiceStatementLineKind.Payment, october.Number, 0m, 40m, 110m)],
            statement.Lines.Select(line => (line.Kind, line.DocumentNumber, line.Charge, line.Credit, line.Balance)));
    }

    [Fact]
    public async Task AnOpenItemStatement_ListsWhatIsOwed_ByHowLateItIs()
    {
        // Arrange: $60 still due on an invoice 13 days late, $50 not due yet, and an invoice in euros.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var late = await IssueAsync(harness, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 100m);
        var notDue = await IssueAsync(harness, new DateOnly(2026, 11, 5), new DateOnly(2026, 12, 5), 50m);
        await PayAsync(harness, late, 40m);
        await IssueAsync(harness, new DateOnly(2026, 11, 7), new DateOnly(2026, 12, 7), 80m, "EUR");

        // Act
        var statement = await BuildAsync(harness, new InvoiceStatementRequest
        {
            Party = _customer,
            Kind = InvoiceStatementKind.OpenItem,
            To = new DateOnly(2026, 11, 13),
            Currency = "USD",
        });
        var document = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceStatementService>().BuildDocumentAsync(statement, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([(late.Number, 13), (notDue.Number, 0)], statement.OpenItems.Select(item => (item.Invoice.Number, item.DaysOverdue)));
        Assert.Equal([50m, 60m, 0m, 0m, 0m], statement.Aging);
        Assert.Equal(110m, statement.ClosingBalance);
        Assert.Equal(["EUR"], statement.OtherCurrencies);
        Assert.Equal((InvoicingConstants.BusinessDocumentKinds.Statement, 2), (document.Kind, document.Rows.Count));
    }

    [Fact]
    public async Task ADebtCanceledOnTheLedger_IsCredited()
    {
        // Arrange: $80 invoiced, then its debt canceled on the ledger.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), 80m);
        var canceled = await harness.OnLedgerAsync(ledger => ledger.CancelAsync(invoice.TransactionId, "Raised in error", TestContext.Current.CancellationToken));

        // Act
        var statement = await BuildAsync(harness, new InvoiceStatementRequest { Party = _customer, Kind = InvoiceStatementKind.Activity, From = new DateOnly(2026, 11, 1), To = new DateOnly(2026, 11, 30) });

        // Assert
        Assert.True(canceled.Succeeded, string.Join(" ", canceled.Errors));
        Assert.Equal(0m, statement.ClosingBalance);
        Assert.Equal([(80m, 0m), (0m, 80m)], statement.Lines.Select(line => (line.Charge, line.Credit)));
    }

    private static Task<InvoiceStatement> BuildAsync(InvoicingTestHarness harness, InvoiceStatementRequest request)
        => harness.InScopeAsync(services => services.GetRequiredService<IInvoiceStatementService>().BuildAsync(request, TestContext.Current.CancellationToken));

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, DateOnly issueDate, DateOnly dueDate, decimal amount, string currency = "USD")
    {
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.Currency = currency;
            invoice.IssueDate = issueDate;
            invoice.DueDate = dueDate;
            invoice.BillTo = new DocumentParty { Party = _customer, Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            invoice.Lines[0].Quantity = 1m;
            invoice.Lines[0].UnitPrice = amount;
        });

        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static async Task PayAsync(InvoicingTestHarness harness, Invoice invoice, decimal amount)
    {
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(invoice.TransactionId, amount, "check", null, TestContext.Current.CancellationToken));

        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
    }
}
