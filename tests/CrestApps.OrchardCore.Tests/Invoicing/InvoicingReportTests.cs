using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoicing reports sum per currency, leave voided invoices out, take credit notes off what was invoiced, count a
/// payment taken back as not collected, and age what is owed by its due date.
/// </summary>
public sealed class InvoicingReportTests
{
    private static readonly DateOnly _today = new(2026, 11, 13);

    [Theory]
    [InlineData(5, 0)]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(-30, 1)]
    [InlineData(-31, 2)]
    [InlineData(-60, 2)]
    [InlineData(-61, 3)]
    [InlineData(-90, 3)]
    [InlineData(-91, 4)]
    public void Aging_PutsAnInvoiceInTheBucketOfHowLongItIsPastDue(int dueInDays, int bucket)
    {
        // Arrange
        var invoice = Open("A", "USD", 10m, due: _today.AddDays(dueInDays));

        // Act
        var result = InvoicingReportMath.Bucket(invoice, _today);

        // Assert
        Assert.Equal(bucket, result);
    }

    [Fact]
    public void Aging_SumsWhatIsStillOwed_PerCustomerAndCurrency_AndSkipsWhatIsNot()
    {
        // Arrange
        var invoices = new[]
        {
            Open("Fabrikam", "USD", 100m, due: _today.AddDays(10)),
            Open("Fabrikam", "USD", 40m, due: _today.AddDays(-45)),
            Open("Fabrikam", "EUR", 70m, due: _today.AddDays(-100)),
            Open("Contoso", "USD", 0m, due: _today.AddDays(-5)),
            Open("Contoso", "USD", 30m, due: _today.AddDays(-5)),
        };
        invoices[4].Status = InvoiceStatus.Void;

        // Act
        var rows = InvoicingReportMath.Aging(invoices, _today);

        // Assert
        Assert.Equal(2, rows.Count);
        var eur = Assert.Single(rows, row => row.Currency == "EUR");
        Assert.Equal([0m, 0m, 0m, 0m, 70m], eur.Buckets);
        var usd = Assert.Single(rows, row => row.Currency == "USD");
        Assert.Equal(("Fabrikam", 140m, 2), (usd.Customer, usd.Total, usd.Invoices));
        Assert.Equal([100m, 0m, 40m, 0m, 0m], usd.Buckets);
    }

    [Fact]
    public void SalesByCustomer_TakesCreditNotesOff_AndLeavesVoidedInvoicesOut()
    {
        // Arrange
        var paid = Issued("Fabrikam", "USD", net: 100m, tax: 20m);
        var voided = Issued("Fabrikam", "USD", net: 500m, tax: 100m);
        voided.Status = InvoiceStatus.Void;
        var euro = Issued("Fabrikam", "EUR", net: 50m, tax: 0m);
        var credit = new CreditNote { Status = CreditNoteStatus.Issued, Currency = "USD", BillTo = paid.BillTo, Totals = new DocumentTotals { TaxTotal = 2m, Total = 12m } };

        // Act
        var rows = InvoicingReportMath.SalesByCustomer([paid, voided, euro], [credit]);

        // Assert
        var usd = Assert.Single(rows, row => row.Currency == "USD");
        Assert.Equal((1, 90m, 18m, 108m, 12m), (usd.Documents, usd.Net, usd.Tax, usd.Total, usd.Credited));
        Assert.Equal(50m, Assert.Single(rows, row => row.Currency == "EUR").Net);
    }

    [Fact]
    public void SalesByItem_GroupsLinesByProductSkuOrName_LessCreditNotes_AndSkipsTextLines()
    {
        // Arrange
        var first = Issued("A", "USD", 0m, 0m);
        first.Lines =
        [
            new DocumentLine { Name = "Widget", ProductId = "p1", Quantity = 2m, Net = 20m },
            new DocumentLine { Name = "Consulting", Quantity = 3m, Net = 300m, DocumentDiscount = 30m },
            new DocumentLine { Name = "Thanks!", Kind = DocumentLineKind.Text },
        ];
        var second = Issued("B", "USD", 0m, 0m);
        second.Lines = [new DocumentLine { Name = "Widget (blue)", ProductId = "p1", Quantity = 1m, Net = 10m }, new DocumentLine { Name = "consulting ", Quantity = 1m, Net = 100m }];

        // Act
        var credit = new CreditNote { Status = CreditNoteStatus.Issued, Currency = "USD", Lines = [new DocumentLine { Name = "Widget", ProductId = "p1", Quantity = 1m, Net = 10m }] };
        var rows = InvoicingReportMath.SalesByItem([first, second], [credit]);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(("Consulting", 4m, 370m, 2), (rows[0].Name, rows[0].Quantity, rows[0].Net, rows[0].Invoices));
        Assert.Equal(("Widget", 2m, 20m, 2), (rows[1].Name, rows[1].Quantity, rows[1].Net, rows[1].Invoices));
    }

    [Fact]
    public void TaxSummary_SumsPerTaxAndRate_LessWhatCreditNotesGaveBack()
    {
        // Arrange
        var invoice = Issued("A", "USD", 0m, 0m);
        invoice.Taxes = [new DocumentTax { Name = "VAT", Rate = 20m, TaxableAmount = 100m, TaxAmount = 20m }, new DocumentTax { Name = "VAT", Rate = 5m, TaxableAmount = 40m, TaxAmount = 2m }];
        var credit = new CreditNote { Status = CreditNoteStatus.Issued, Currency = "USD", Taxes = [new DocumentTax { Name = "VAT", Rate = 20m, TaxableAmount = 10m, TaxAmount = 2m }] };

        // Act
        var rows = InvoicingReportMath.TaxSummary([invoice], [credit]);

        // Assert
        var standard = Assert.Single(rows, row => row.Rate == 20m);
        Assert.Equal((90m, 18m, 2m), (standard.Taxable, standard.Tax, standard.CreditedTax));
        Assert.Equal(2m, Assert.Single(rows, row => row.Rate == 5m).Tax);
    }

    [Fact]
    public void Payments_ListsWhatWasReceivedInThePeriod_LeavingOutAPaymentTakenBack()
    {
        // Arrange
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var transaction = new Transaction
        {
            InvoiceNumber = "INV-1",
            Currency = "USD",
            Events =
            [
                new TransactionEvent { Id = "e1", Type = TransactionEventType.PaymentRecorded, Amount = 50m, CreatedUtc = from.AddDays(1), PaymentMethod = "Check" },
                new TransactionEvent { Id = "e2", Type = TransactionEventType.PaymentRecorded, Amount = 25m, CreatedUtc = from.AddDays(2) },
                new TransactionEvent { Id = "e3", Type = TransactionEventType.Reversed, Amount = 25m, CreatedUtc = from.AddDays(3), ReversesEventId = "e2" },
                new TransactionEvent { Id = "e4", Type = TransactionEventType.Refunded, Amount = 10m, CreatedUtc = from.AddDays(4) },
                new TransactionEvent { Id = "e5", Type = TransactionEventType.PaymentRecorded, Amount = 99m, CreatedUtc = from.AddDays(-1) },
            ],
        };

        // Act
        var rows = InvoicingReportMath.Payments([transaction], from, to);

        // Assert
        Assert.Equal([(50m, false, "Check"), (10m, true, null)], rows.Select(row => (row.Amount, row.IsRefund, row.PaymentMethod)));
    }

    [Fact]
    public async Task EveryReport_RunsOnIssuedPaidAndCreditedInvoices()
    {
        // Arrange: one invoice paid in part, one credited in full.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var paid = await IssueAsync(harness);
        var credited = await IssueAsync(harness);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(paid.TransactionId, new TransactionPaymentRequest { Amount = 40m, PaymentMethod = "Check" }, TestContext.Current.CancellationToken));
        var draft = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(credited.ItemId, TestContext.Current.CancellationToken));
        await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.CreditNote.ItemId, TestContext.Current.CancellationToken));

        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = InvoicingTestHarness.Now.AddDays(-1), ToUtc = InvoicingTestHarness.Now.AddDays(1) });
        var context = new ReportContext(filter);

        string Money(decimal amount) => harness.InScopeAsync(services => Task.FromResult(services.GetRequiredService<IMoneyFormatter>().Format(amount, "USD"))).GetAwaiter().GetResult();

        // Act
        var documents = await harness.InScopeAsync(async services =>
        {
            var reader = new InvoicingReportReader(services.GetRequiredService<ISession>(), services.GetRequiredService<IClock>());
            var money = services.GetRequiredService<IMoneyFormatter>();
            var localClock = services.GetRequiredService<ILocalClock>();
            IReport[] reports =
            [
                new ReceivablesAgingReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<ReceivablesAgingReport>()),
                new InvoiceRegisterReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<InvoiceRegisterReport>()),
                new PaymentsCollectedReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<PaymentsCollectedReport>()),
                new SalesByCustomerReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<SalesByCustomerReport>()),
                new SalesByItemReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<SalesByItemReport>()),
                new TaxSummaryReport(reader, money, localClock, new Telephony.Doubles.PassThroughStringLocalizer<TaxSummaryReport>()),
            ];

            var results = new List<ReportDocument>();

            foreach (var report in reports)
            {
                results.Add(await report.RunAsync(context, TestContext.Current.CancellationToken));
            }

            return results;
        });

        // Assert
        static ReportSection Table(ReportDocument document)
            => document.Sections.Last(section => section.Rows is { Count: > 0 });

        var aging = Assert.Single(Table(documents[0]).Rows);
        Assert.Equal(["Fabrikam Ltd.", "1", Money(60m), Money(0m), Money(0m), Money(0m), Money(0m), Money(60m)], aging.Cells);

        var register = Table(documents[1]);
        Assert.Equal(2, register.Rows.Count);
        Assert.Contains(register.Rows, row => row.Cells[0] == paid.Number && row.Cells.Contains("Partly paid") && row.Cells[^1] == Money(60m));

        var payment = Assert.Single(Table(documents[2]).Rows);
        Assert.Equal((paid.Number, "Payment", "Check", Money(40m)), (payment.Cells[1], payment.Cells[3], payment.Cells[4], payment.Cells[6]));

        var sales = Assert.Single(Table(documents[3]).Rows);
        Assert.Equal(["Fabrikam Ltd.", "2", Money(100m), Money(0m), Money(100m), Money(100m)], sales.Cells);

        var item = Assert.Single(Table(documents[4]).Rows);
        Assert.Equal(("Consulting", "2", Money(100m)), (item.Cells[0], item.Cells[2], item.Cells[4]));

        // Untaxed lines are kept as a zero-rate tax: what was taxed at 0%, less the credit note.
        var tax = Assert.Single(Table(documents[5]).Rows);
        Assert.Equal((Money(100m), Money(0m)), (tax.Cells[2], tax.Cells[3]));
    }

    private static Invoice Open(string customer, string currency, decimal amountDue, DateOnly due)
        => new()
        {
            Status = InvoiceStatus.Open,
            Currency = currency,
            AmountDue = amountDue,
            DueDate = due,
            BillTo = new DocumentParty { Contact = new CrestApps.OrchardCore.Addresses.Models.ContactInfo { Name = customer } },
        };

    private static Invoice Issued(string customer, string currency, decimal net, decimal tax)
        => new()
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Status = InvoiceStatus.Open,
            Currency = currency,
            BillTo = new DocumentParty { Party = new PartyRef("Account", customer), Contact = new CrestApps.OrchardCore.Addresses.Models.ContactInfo { Name = customer } },
            Totals = new DocumentTotals { Subtotal = net, TaxTotal = tax, Total = net + tax },
        };

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
