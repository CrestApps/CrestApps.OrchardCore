using System.Globalization;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Reports;

/// <summary>
/// Reads the documents the invoicing reports need, a page at a time, through the index columns.
/// </summary>
internal sealed class InvoicingReportReader
{
    private const int PageSize = 500;

    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly IClock _clock;

    public InvoicingReportReader(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    public (DateTime FromUtc, DateTime ToUtc) Range(ReportContext context)
    {
        var range = context.Filter.GetDateRange();

        return (range.FromUtc ?? _earliest, range.ToUtc ?? _clock.UtcNow);
    }

    /// <summary>
    /// The invoices issued in the period.
    /// </summary>
    public Task<List<Invoice>> IssuedAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => PageAsync<Invoice>(skip => _session
            .Query<Invoice, InvoiceIndex>(index => index.IssuedUtc != null && index.IssuedUtc >= fromUtc && index.IssuedUtc < toUtc, collection: InvoicingConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Skip(skip)
            .Take(PageSize)
            .ListAsync(cancellationToken));

    /// <summary>
    /// The open invoices that still owe something.
    /// </summary>
    public Task<List<Invoice>> OpenAsync(CancellationToken cancellationToken)
    {
        var open = (int)InvoiceStatus.Open;

        return PageAsync<Invoice>(skip => _session
            .Query<Invoice, InvoiceIndex>(index => index.Status == open && index.AmountDue > 0m, collection: InvoicingConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Skip(skip)
            .Take(PageSize)
            .ListAsync(cancellationToken));
    }

    /// <summary>
    /// The credit notes issued in the period.
    /// </summary>
    public Task<List<CreditNote>> CreditNotesIssuedAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => PageAsync<CreditNote>(skip => _session
            .Query<CreditNote, CreditNoteIndex>(index => index.IssuedUtc != null && index.IssuedUtc >= fromUtc && index.IssuedUtc < toUtc, collection: InvoicingConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Skip(skip)
            .Take(PageSize)
            .ListAsync(cancellationToken));

    /// <summary>
    /// The transactions invoices posted that changed since the start of the period: those a payment of the period is on.
    /// </summary>
    public Task<List<Transaction>> InvoiceTransactionsChangedSinceAsync(DateTime fromUtc, CancellationToken cancellationToken)
        => PageAsync<Transaction>(skip => _session
            .Query<Transaction, TransactionIndex>(index => index.ReferenceType == InvoicingConstants.InvoiceReferenceType && index.UpdatedUtc >= fromUtc, collection: TransactionsConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Skip(skip)
            .Take(PageSize)
            .ListAsync(cancellationToken));

    private static async Task<List<T>> PageAsync<T>(Func<int, Task<IReadOnlyList<T>>> read)
    {
        var all = new List<T>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await read(skip)).ToList();

            all.AddRange(page);

            if (page.Count < PageSize)
            {
                return all;
            }
        }
    }
}

/// <summary>
/// The base of the invoicing reports: reading, money and dates.
/// </summary>
internal abstract class InvoicingReportBase
{
    protected InvoicingReportBase(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer stringLocalizer)
    {
        Reader = reader;
        MoneyFormatter = money;
        LocalClock = localClock;
        S = stringLocalizer;
    }

    protected InvoicingReportReader Reader { get; }

    protected IMoneyFormatter MoneyFormatter { get; }

    protected ILocalClock LocalClock { get; }

    protected IStringLocalizer S { get; }

    protected string Money(decimal amount, string currency)
        => MoneyFormatter.Format(amount, currency);

    protected static string Date(DateOnly? date)
        => date?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

    protected async Task<string> LocalTimeAsync(DateTime utc)
        => (await LocalClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).ToString("g", CultureInfo.CurrentCulture);

    protected async Task<DateOnly> LocalTodayAsync()
        => DateOnly.FromDateTime((await LocalClock.GetLocalNowAsync()).DateTime);

    protected ReportDocument Empty(ReportDocument document, string message)
    {
        document.Add(ReportSection.ForMetrics(S["Summary"].Value, [new ReportMetric(S["Nothing to show"].Value, "0", message)]));

        return document;
    }
}

/// <summary>
/// What open invoices still owe, per customer and currency, by how long it is past due today.
/// </summary>
internal sealed class ReceivablesAgingReport : InvoicingReportBase, IReport
{
    public ReceivablesAgingReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<ReceivablesAgingReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-receivables-aging";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ReceivablesAgingReport>("Receivables aging");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<ReceivablesAgingReport>("What open invoices still owe today, per customer, by how long it is past due: not due yet, 1 to 30, 31 to 60, 61 to 90 and over 90 days.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var today = await LocalTodayAsync();
        var rows = InvoicingReportMath.Aging(await Reader.OpenAsync(cancellationToken), today);
        var document = new ReportDocument { Title = DisplayName.Value };

        if (rows.Count == 0)
        {
            return Empty(document, S["No open invoice owes anything."].Value);
        }

        document.Add(ReportSection.ForMetrics(S["Owed today, {0}", Date(today)].Value, [.. rows
            .GroupBy(row => row.Currency)
            .Select(group => new ReportMetric(
                S["Owed in {0}", group.Key].Value,
                Money(group.Sum(row => row.Total), group.Key),
                S["{0} past due", Money(group.Sum(row => row.Total - row.Buckets[0]), group.Key)].Value))]));

        document.Add(ReportSection.ForTable(S["By customer"].Value,
        [
            new ReportColumn(S["Customer"].Value),
            new ReportColumn(S["Invoices"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Not due yet"].Value, ReportColumnAlign.End),
            new ReportColumn(S["1-30 days"].Value, ReportColumnAlign.End),
            new ReportColumn(S["31-60 days"].Value, ReportColumnAlign.End),
            new ReportColumn(S["61-90 days"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Over 90 days"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
        ], [.. rows.Select(row => new ReportRow(
        [
            row.Customer,
            ReportFormat.Number(row.Invoices),
            .. row.Buckets.Select(amount => Money(amount, row.Currency)),
            Money(row.Total, row.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// Every invoice issued in the period, with its customer, totals and what is still due.
/// </summary>
internal sealed class InvoiceRegisterReport : InvoicingReportBase, IReport
{
    public InvoiceRegisterReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<InvoiceRegisterReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-invoice-register";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<InvoiceRegisterReport>("Invoice register");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<InvoiceRegisterReport>("Every invoice issued in the period, in number order, with its customer, tax, total, what was paid and credited, and what is still due.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = Reader.Range(context);
        var invoices = (await Reader.IssuedAsync(from, to, cancellationToken))
            .OrderBy(invoice => invoice.IssueDate)
            .ThenBy(invoice => invoice.Number, StringComparer.Ordinal)
            .ToList();

        var document = new ReportDocument { Title = DisplayName.Value };

        if (invoices.Count == 0)
        {
            return Empty(document, S["No invoice was issued in the period."].Value);
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, [
            new ReportMetric(S["Invoices issued"].Value, ReportFormat.Number(invoices.Count)),
            new ReportMetric(S["Voided"].Value, ReportFormat.Number(invoices.Count(invoice => invoice.Status == InvoiceStatus.Void))),
            .. invoices
                .Where(invoice => invoice.Status != InvoiceStatus.Void)
                .GroupBy(invoice => invoice.Currency ?? string.Empty)
                .Select(group => new ReportMetric(S["Invoiced in {0}", group.Key].Value, Money(group.Sum(invoice => invoice.Totals?.Total ?? 0m), group.Key), S["{0} still due", Money(group.Sum(invoice => invoice.AmountDue), group.Key)].Value)),
        ]));

        document.Add(ReportSection.ForTable(S["Invoices"].Value,
        [
            new ReportColumn(S["Number"].Value),
            new ReportColumn(S["Issued"].Value),
            new ReportColumn(S["Due"].Value),
            new ReportColumn(S["Customer"].Value),
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Paid"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Credited"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Still due"].Value, ReportColumnAlign.End),
        ], [.. invoices.Select(invoice => new ReportRow(
        [
            invoice.Number ?? string.Empty,
            Date(invoice.IssueDate),
            Date(invoice.DueDate),
            invoice.BillTo?.Contact?.Name ?? string.Empty,
            StatusText(invoice),
            Money(invoice.Totals?.TaxTotal ?? 0m, invoice.Currency),
            Money(invoice.Totals?.Total ?? 0m, invoice.Currency),
            Money(invoice.AmountPaid, invoice.Currency),
            Money(invoice.AmountCredited, invoice.Currency),
            Money(invoice.AmountDue, invoice.Currency),
        ]))]));

        return document;
    }

    private string StatusText(Invoice invoice)
        => invoice.Status switch
        {
            InvoiceStatus.Void => S["Voided"].Value,
            InvoiceStatus.Uncollectible => S["Written off"].Value,
            _ => invoice.Settlement switch
            {
                InvoiceSettlement.Paid => S["Paid"].Value,
                InvoiceSettlement.PartiallyPaid => S["Partly paid"].Value,
                _ => S["Unpaid"].Value,
            },
        };
}

/// <summary>
/// The payments recorded on invoices in the period, each less what was taken back of it, and the refunds given.
/// </summary>
internal sealed class PaymentsCollectedReport : InvoicingReportBase, IReport
{
    public PaymentsCollectedReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<PaymentsCollectedReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-payments-collected";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PaymentsCollectedReport>("Payments collected");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<PaymentsCollectedReport>("The payments received toward invoices in the period, online or recorded by staff, by method, with the refunds given.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = Reader.Range(context);
        var rows = InvoicingReportMath.Payments(await Reader.InvoiceTransactionsChangedSinceAsync(from, cancellationToken), from, to);
        var document = new ReportDocument { Title = DisplayName.Value };

        if (rows.Count == 0)
        {
            return Empty(document, S["No payment was received toward an invoice in the period."].Value);
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, [.. rows
            .GroupBy(row => row.Currency ?? string.Empty)
            .Select(group => new ReportMetric(
                S["Collected in {0}", group.Key].Value,
                Money(group.Where(row => !row.IsRefund).Sum(row => row.Amount) - group.Where(row => row.IsRefund).Sum(row => row.Amount), group.Key),
                S["{0} received, {1} refunded", Money(group.Where(row => !row.IsRefund).Sum(row => row.Amount), group.Key), Money(group.Where(row => row.IsRefund).Sum(row => row.Amount), group.Key)].Value))]));

        document.Add(ReportSection.ForTable(S["By method"].Value,
        [
            new ReportColumn(S["Method"].Value),
            new ReportColumn(S["Payments"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Received"].Value, ReportColumnAlign.End),
        ], [.. rows
            .Where(row => !row.IsRefund)
            .GroupBy(row => (Method: MethodName(row), Currency: row.Currency ?? string.Empty))
            .OrderBy(group => group.Key.Currency, StringComparer.Ordinal)
            .ThenByDescending(group => group.Sum(row => row.Amount))
            .Select(group => new ReportRow([group.Key.Method, ReportFormat.Number(group.Count()), Money(group.Sum(row => row.Amount), group.Key.Currency)]))]));

        var lines = new List<ReportRow>();

        foreach (var row in rows)
        {
            lines.Add(new ReportRow(
            [
                await LocalTimeAsync(row.WhenUtc),
                row.InvoiceNumber ?? string.Empty,
                row.Customer ?? string.Empty,
                row.IsRefund ? S["Refund"].Value : S["Payment"].Value,
                MethodName(row),
                row.Reference ?? string.Empty,
                Money(row.IsRefund ? -row.Amount : row.Amount, row.Currency),
            ]));
        }

        document.Add(ReportSection.ForTable(S["Payments and refunds"].Value,
        [
            new ReportColumn(S["When"].Value),
            new ReportColumn(S["Invoice"].Value),
            new ReportColumn(S["Customer"].Value),
            new ReportColumn(S["Kind"].Value),
            new ReportColumn(S["Method"].Value),
            new ReportColumn(S["Reference"].Value),
            new ReportColumn(S["Amount"].Value, ReportColumnAlign.End),
        ], lines));

        return document;
    }

    // How the money arrived: the method staff chose for a payment recorded by hand, otherwise how it was settled.
    private string MethodName(PaymentRow row)
        => !string.IsNullOrEmpty(row.PaymentMethod) ? OfflinePaymentMethodText.Name(row.PaymentMethod, S)
            : string.Equals(row.SettlementMethod, TransactionsConstants.SettlementMethods.Online, StringComparison.OrdinalIgnoreCase) ? S["Online"].Value
            : string.Equals(row.SettlementMethod, TransactionsConstants.SettlementMethods.CustomerCredit, StringComparison.OrdinalIgnoreCase) ? S["Customer credit"].Value
            : string.IsNullOrEmpty(row.SettlementMethod) ? S["Not given"].Value
            : row.SettlementMethod;
}

/// <summary>
/// What was invoiced to each customer in the period, less what credit notes took off.
/// </summary>
internal sealed class SalesByCustomerReport : InvoicingReportBase, IReport
{
    public SalesByCustomerReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<SalesByCustomerReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-sales-by-customer";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SalesByCustomerReport>("Invoiced sales by customer");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SalesByCustomerReport>("What was invoiced to each customer in the period, before and after tax, less the credit notes issued in the period. Voided invoices count for nothing.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = Reader.Range(context);
        var rows = InvoicingReportMath.SalesByCustomer(await Reader.IssuedAsync(from, to, cancellationToken), await Reader.CreditNotesIssuedAsync(from, to, cancellationToken));
        var document = new ReportDocument { Title = DisplayName.Value };

        if (rows.Count == 0)
        {
            return Empty(document, S["Nothing was invoiced in the period."].Value);
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, [.. rows
            .GroupBy(row => row.Currency)
            .Select(group => new ReportMetric(S["Invoiced in {0}, before tax", group.Key].Value, Money(group.Sum(row => row.Net), group.Key), S["{0} customers", group.Count()].Value))]));

        document.Add(ReportSection.ForTable(S["By customer"].Value,
        [
            new ReportColumn(S["Customer"].Value),
            new ReportColumn(S["Invoices"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Before tax"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Credited"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
        ], [.. rows.Select(row => new ReportRow(
        [
            row.Customer,
            ReportFormat.Number(row.Documents),
            Money(row.Net, row.Currency),
            Money(row.Tax, row.Currency),
            Money(row.Credited, row.Currency),
            Money(row.Total, row.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// What was invoiced of each item in the period: how many and for how much before tax.
/// </summary>
internal sealed class SalesByItemReport : InvoicingReportBase, IReport
{
    public SalesByItemReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<SalesByItemReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-sales-by-item";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SalesByItemReport>("Invoiced sales by item");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SalesByItemReport>("What was invoiced of each product, service or other line in the period: the quantity and the amount before tax, after discounts, less the credit notes issued in the period. Voided invoices count for nothing.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = Reader.Range(context);
        var rows = InvoicingReportMath.SalesByItem(await Reader.IssuedAsync(from, to, cancellationToken), await Reader.CreditNotesIssuedAsync(from, to, cancellationToken));
        var document = new ReportDocument { Title = DisplayName.Value };

        if (rows.Count == 0)
        {
            return Empty(document, S["Nothing was invoiced in the period."].Value);
        }

        document.Add(ReportSection.ForTable(S["By item"].Value,
        [
            new ReportColumn(S["Item"].Value),
            new ReportColumn(S["SKU"].Value),
            new ReportColumn(S["Invoices"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Quantity"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Before tax"].Value, ReportColumnAlign.End),
        ], [.. rows.Select(row => new ReportRow(
        [
            row.Name,
            row.Sku ?? string.Empty,
            ReportFormat.Number(row.Invoices),
            row.Quantity.ToString("0.####", CultureInfo.CurrentCulture),
            Money(row.Net, row.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// The taxes invoiced in the period per tax and rate, less what credit notes gave back.
/// </summary>
internal sealed class TaxSummaryReport : InvoicingReportBase, IReport
{
    public TaxSummaryReport(InvoicingReportReader reader, IMoneyFormatter money, ILocalClock localClock, IStringLocalizer<TaxSummaryReport> stringLocalizer)
        : base(reader, money, localClock, stringLocalizer)
    {
    }

    public static string Name => "invoicing-tax-summary";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TaxSummaryReport>("Invoiced tax summary");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TaxSummaryReport>("The tax on invoices issued in the period, per tax and rate, with the amount taxed, less what credit notes issued in the period gave back. Voided invoices count for nothing.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission => InvoicingPermissions.ViewInvoices;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = Reader.Range(context);
        var rows = InvoicingReportMath.TaxSummary(await Reader.IssuedAsync(from, to, cancellationToken), await Reader.CreditNotesIssuedAsync(from, to, cancellationToken));
        var document = new ReportDocument { Title = DisplayName.Value };

        if (rows.Count == 0)
        {
            return Empty(document, S["No tax was invoiced in the period."].Value);
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, [.. rows
            .GroupBy(row => row.Currency)
            .Select(group => new ReportMetric(S["Tax in {0}", group.Key].Value, Money(group.Sum(row => row.Tax), group.Key), S["{0} given back by credit notes", Money(group.Sum(row => row.CreditedTax), group.Key)].Value))]));

        document.Add(ReportSection.ForTable(S["By tax"].Value,
        [
            new ReportColumn(S["Tax"].Value),
            new ReportColumn(S["Rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Amount taxed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Of which credited"].Value, ReportColumnAlign.End),
        ], [.. rows.Select(row => new ReportRow(
        [
            row.Name,
            row.Rate.ToString("0.####", CultureInfo.CurrentCulture) + "%",
            Money(row.Taxable, row.Currency),
            Money(row.Tax, row.Currency),
            Money(row.CreditedTax, row.Currency),
        ]))]));

        return document;
    }
}
