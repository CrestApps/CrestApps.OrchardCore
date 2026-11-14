using System.Linq.Expressions;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Reports;

/// <summary>
/// One data set of the invoicing data source.
/// </summary>
public interface IInvoicingReportDataSet : IRecordDataSet
{
}

/// <summary>
/// Exposes invoices, their lines and taxes, and credit notes as a data source of Orchard Core, for reports and data
/// pipelines. Each data set needs the View invoices permission. Money is in the document's own currency, which each row
/// names; issue and due dates are the documents' own dates.
/// </summary>
public sealed class InvoicingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IInvoicingReportDataSet[] _dataSets;

    public InvoicingReportDataSource(IEnumerable<IInvoicingReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.InvoicingSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<InvoicingReportDataSource>("Invoicing");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<InvoicingReportDataSource>("Invoices with their lines and taxes, and credit notes.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

internal static class InvoicingReportFields
{
    public const string InvoiceIdField = "InvoiceId";

    public const string CreatedUtcField = "CreatedUtc";

    public static DateTime? ToDate(DateOnly? date)
        => date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    public static PartyRef Party(InvoicingDocument document)
        => document.BillTo?.Party is { IsEmpty: false } party ? party : default;
}

/// <summary>
/// One row per invoice: its number, status, totals, what was paid, credited and is still due, who is billed and its
/// dates.
/// </summary>
public sealed class InvoicesReportDataSet : IndexedReportDataSet<Invoice, InvoiceIndex>, IInvoicingReportDataSet
{
    public InvoicesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<InvoicesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.Invoices,
                stringLocalizer["Invoices"],
                stringLocalizer["One row per invoice: number, status, totals, paid, credited and still due in its own currency, who is billed and its dates."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var invoice = S["Invoice"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var billTo = S["Bill to"].Value;
        var inCurrency = S["In the invoice's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Invoice ID"], DataFieldType.Text, record => record.ItemId, invoice).Identifier();
        AddField(nameof(Invoice.Number), S["Number"], DataFieldType.Text, record => record.Number, invoice);
        AddField(nameof(Invoice.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, invoice);
        AddField(nameof(Invoice.Status), S["Status"], DataFieldType.Text, record => record.Status, invoice);
        AddField(nameof(Invoice.Settlement), S["Payment status"], DataFieldType.Text, record => record.Settlement, invoice);
        AddField(nameof(Invoice.Delivery), S["Delivery"], DataFieldType.Text, record => record.Delivery, invoice);
        AddField(nameof(Invoice.Reference), S["Reference"], DataFieldType.Text, record => record.Reference, invoice);
        AddField(nameof(Invoice.PurchaseOrderNumber), S["Purchase order"], DataFieldType.Text, record => record.PurchaseOrderNumber, invoice);
        AddField(nameof(Invoice.PaymentTermName), S["Payment term"], DataFieldType.Text, record => record.PaymentTermName, invoice);
        AddField(nameof(Invoice.SourceType), S["Billed from"], DataFieldType.Text, record => record.SourceType, invoice);
        AddField(nameof(Invoice.SourceId), S["Billed from ID"], DataFieldType.Text, record => record.SourceId, invoice).Identifier();
        AddField(nameof(Invoice.TransactionId), S["Transaction ID"], DataFieldType.Text, record => record.TransactionId, invoice)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions));

        AddField(nameof(Invoice.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField("Subtotal", S["Subtotal"], DataFieldType.Decimal, record => record.Totals?.Subtotal ?? 0m, money).Description = inCurrency;
        AddField("DiscountTotal", S["Discounts"], DataFieldType.Decimal, record => record.Totals?.DiscountTotal ?? 0m, money).Description = inCurrency;
        AddField("ChargeTotal", S["Charges"], DataFieldType.Decimal, record => record.Totals?.ChargeTotal ?? 0m, money).Description = inCurrency;
        AddField("TaxTotal", S["Tax"], DataFieldType.Decimal, record => record.Totals?.TaxTotal ?? 0m, money).Description = inCurrency;
        AddField("Total", S["Total"], DataFieldType.Decimal, record => record.Totals?.Total ?? 0m, money).Description = inCurrency;
        AddField(nameof(Invoice.AmountPaid), S["Paid"], DataFieldType.Decimal, record => record.AmountPaid, money).Description = inCurrency;
        AddField(nameof(Invoice.AmountCredited), S["Credited"], DataFieldType.Decimal, record => record.AmountCredited, money).Description = inCurrency;
        AddField(nameof(Invoice.AmountDue), S["Still due"], DataFieldType.Decimal, record => record.AmountDue, money).Description = inCurrency;

        AddField("IssueDate", S["Issue date"], DataFieldType.Date, record => InvoicingReportFields.ToDate(record.IssueDate), dates);
        AddField("DueDate", S["Due date"], DataFieldType.Date, record => InvoicingReportFields.ToDate(record.DueDate), dates);
        AddField(InvoicingReportFields.CreatedUtcField, S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Invoice.IssuedUtc), S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc, dates);
        AddField(nameof(Invoice.SentUtc), S["Sent"], DataFieldType.DateTime, record => record.SentUtc, dates);
        AddField(nameof(Invoice.ViewedUtc), S["Viewed"], DataFieldType.DateTime, record => record.ViewedUtc, dates);
        AddField(nameof(Invoice.VoidedUtc), S["Voided"], DataFieldType.DateTime, record => record.VoidedUtc, dates);

        AddField("BillToKind", S["Bill-to kind"], DataFieldType.Text, record => InvoicingReportFields.Party(record).Kind, billTo).Description = S["The kind of party billed, such as Account, Customer or User."].Value;
        AddField("BillToId", S["Bill-to ID"], DataFieldType.Text, record => InvoicingReportFields.Party(record).Id, billTo).Identifier();
        AddField("BillToName", S["Bill-to name"], DataFieldType.Text, record => record.BillTo?.Contact?.Name, billTo);
        AddField("BillToEmail", S["Bill-to email"], DataFieldType.Text, record => record.BillTo?.Contact?.Email, billTo);
        AddField("BillToTaxId", S["Bill-to tax ID"], DataFieldType.Text, record => record.BillTo?.TaxId, billTo);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InvoiceIndex, DateTime>> Column)? DateColumn
        => (InvoicingReportFields.CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InvoiceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InvoiceIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Invoice.TransactionId)] = index => index.TransactionId,
            [nameof(Invoice.SourceId)] = index => index.SourceId,
            ["BillToId"] = index => index.BillToId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InvoiceIndex> columns)
    {
        columns
            .Text(nameof(Invoice.Currency), nameof(InvoiceIndex.Currency))
            .Text(nameof(Invoice.SourceType), nameof(InvoiceIndex.SourceType));
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps a shortened bill-to name.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove("BillToName");

        return columns;
    }
}

/// <summary>
/// One row per line of an issued invoice: what was billed, how many, at what price, its tax and total, with the
/// invoice's number, currency, bill-to and issue date. Drafts give no rows.
/// </summary>
public sealed class InvoiceLinesReportDataSet : IndexedReportDataSet<Invoice, InvoiceIndex, ReportChildRow<Invoice, DocumentLine>>, IInvoicingReportDataSet
{
    public InvoiceLinesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<InvoiceLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.InvoiceLines,
                stringLocalizer["Invoice lines"],
                stringLocalizer["One row per line of an issued invoice: the item or service, quantity, price, discount, tax and total in the invoice's currency."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var invoice = S["Invoice"].Value;
        var line = S["Line"].Value;
        var inCurrency = S["In the invoice's currency."].Value;

        AddField(InvoicingReportFields.InvoiceIdField, S["Invoice ID"], DataFieldType.Text, row => row.Parent.ItemId, invoice)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InvoicingSource.Name, CommerceReportDataSets.InvoicingSource.Invoices));
        AddField("InvoiceNumber", S["Invoice number"], DataFieldType.Text, row => row.Parent.Number, invoice);
        AddField("InvoiceStatus", S["Invoice status"], DataFieldType.Text, row => row.Parent.Status, invoice);
        AddField(nameof(Invoice.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, invoice);
        AddField("IssueDate", S["Issue date"], DataFieldType.Date, row => InvoicingReportFields.ToDate(row.Parent.IssueDate), invoice);
        AddField("BillToKind", S["Bill-to kind"], DataFieldType.Text, row => InvoicingReportFields.Party(row.Parent).Kind, invoice);
        AddField("BillToId", S["Bill-to ID"], DataFieldType.Text, row => InvoicingReportFields.Party(row.Parent).Id, invoice).Identifier();
        AddField("BillToName", S["Bill-to name"], DataFieldType.Text, row => row.Parent.BillTo?.Contact?.Name, invoice);

        AddField("LineId", S["Line ID"], DataFieldType.Text, row => row.Child.Id, line).Identifier();
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField("LineKind", S["Line kind"], DataFieldType.Text, row => row.Child.Kind, line);
        AddField(nameof(DocumentLine.ProductId), S["Product ID"], DataFieldType.Text, row => row.Child.ProductId, line).Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(DocumentLine.Sku), S["SKU"], DataFieldType.Text, row => row.Child.Sku, line);
        AddField(nameof(DocumentLine.Name), S["Name"], DataFieldType.Text, row => row.Child.Name, line);
        AddField(nameof(DocumentLine.Quantity), S["Quantity"], DataFieldType.Decimal, row => row.Child.Quantity, line);
        AddField(nameof(DocumentLine.Unit), S["Unit"], DataFieldType.Text, row => row.Child.Unit, line);
        AddField(nameof(DocumentLine.UnitPrice), S["Price each"], DataFieldType.Decimal, row => row.Child.UnitPrice, line).Description = inCurrency;
        AddField(nameof(DocumentLine.Net), S["Net"], DataFieldType.Decimal, row => row.Child.Net, line).Description = S["After the line's discount, before the invoice's discounts and tax, in the invoice's currency."].Value;
        AddField(nameof(DocumentLine.DocumentDiscount), S["Share of invoice discounts"], DataFieldType.Decimal, row => row.Child.DocumentDiscount, line).Description = inCurrency;
        AddField(nameof(DocumentLine.TaxAmount), S["Tax"], DataFieldType.Decimal, row => row.Child.TaxAmount, line).Description = inCurrency;
        AddField(nameof(DocumentLine.Total), S["Total"], DataFieldType.Decimal, row => row.Child.Total, line).Description = inCurrency;
        AddField(nameof(DocumentLine.TaxCategoryCode), S["Tax category"], DataFieldType.Text, row => row.Child.TaxCategoryCode, line);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InvoiceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InvoiceIndex, string>>>(StringComparer.Ordinal)
        {
            [InvoicingReportFields.InvoiceIdField] = index => index.ItemId,
            ["BillToId"] = index => index.BillToId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Invoice, DocumentLine>> GetRows(Invoice document)
        => document.Status == InvoiceStatus.Draft ? [] : ReportChildRow<Invoice, DocumentLine>.For(document, document.Lines);
}

/// <summary>
/// One row per tax of an issued invoice, at one rate: what was taxed and the tax, with the invoice's number, currency,
/// bill-to and issue date. Drafts give no rows.
/// </summary>
public sealed class InvoiceTaxesReportDataSet : IndexedReportDataSet<Invoice, InvoiceIndex, ReportChildRow<Invoice, DocumentTax>>, IInvoicingReportDataSet
{
    public InvoiceTaxesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<InvoiceTaxesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.InvoiceTaxes,
                stringLocalizer["Invoice taxes"],
                stringLocalizer["One row per tax of an issued invoice at one rate: the amount taxed and the tax, in the invoice's currency."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var invoice = S["Invoice"].Value;
        var tax = S["Tax"].Value;
        var inCurrency = S["In the invoice's currency."].Value;

        AddField(InvoicingReportFields.InvoiceIdField, S["Invoice ID"], DataFieldType.Text, row => row.Parent.ItemId, invoice)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InvoicingSource.Name, CommerceReportDataSets.InvoicingSource.Invoices));
        AddField("InvoiceNumber", S["Invoice number"], DataFieldType.Text, row => row.Parent.Number, invoice);
        AddField("InvoiceStatus", S["Invoice status"], DataFieldType.Text, row => row.Parent.Status, invoice);
        AddField(nameof(Invoice.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, invoice);
        AddField("IssueDate", S["Issue date"], DataFieldType.Date, row => InvoicingReportFields.ToDate(row.Parent.IssueDate), invoice);
        AddField("BillToId", S["Bill-to ID"], DataFieldType.Text, row => InvoicingReportFields.Party(row.Parent).Id, invoice).Identifier();
        AddField("BillToTaxId", S["Bill-to tax ID"], DataFieldType.Text, row => row.Parent.BillTo?.TaxId, invoice);

        AddField(nameof(DocumentTax.Code), S["Tax code"], DataFieldType.Text, row => row.Child.Code, tax);
        AddField(nameof(DocumentTax.Name), S["Tax name"], DataFieldType.Text, row => row.Child.Name, tax);
        AddField(nameof(DocumentTax.Treatment), S["Treatment"], DataFieldType.Text, row => row.Child.Treatment, tax);
        AddField(nameof(DocumentTax.Rate), S["Rate"], DataFieldType.Decimal, row => row.Child.Rate, tax).Description = S["In percent, such as 20 for 20%."].Value;
        AddField(nameof(DocumentTax.TaxableAmount), S["Amount taxed"], DataFieldType.Decimal, row => row.Child.TaxableAmount, tax).Description = inCurrency;
        AddField(nameof(DocumentTax.TaxAmount), S["Tax"], DataFieldType.Decimal, row => row.Child.TaxAmount, tax).Description = inCurrency;
        AddField(nameof(DocumentTax.IsCompound), S["Compound"], DataFieldType.Boolean, row => row.Child.IsCompound, tax);
        AddField(nameof(DocumentTax.ExemptionReason), S["Exemption reason"], DataFieldType.Text, row => row.Child.ExemptionReason, tax);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InvoiceIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InvoiceIndex, string>>>(StringComparer.Ordinal)
        {
            [InvoicingReportFields.InvoiceIdField] = index => index.ItemId,
            ["BillToId"] = index => index.BillToId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Invoice, DocumentTax>> GetRows(Invoice document)
        => document.Status == InvoiceStatus.Draft ? [] : ReportChildRow<Invoice, DocumentTax>.For(document, document.Taxes);
}

/// <summary>
/// One row per credit note: its number, the invoice it credits, what became of the amount, and its totals.
/// </summary>
public sealed class CreditNotesReportDataSet : IndexedReportDataSet<CreditNote, CreditNoteIndex>, IInvoicingReportDataSet
{
    public CreditNotesReportDataSet(ISession session, IAuthorizationService authorizationService, IStringLocalizer<CreditNotesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InvoicingSource.CreditNotes,
                stringLocalizer["Credit notes"],
                stringLocalizer["One row per credit note: its number, the invoice it credits, how the amount was given back, and its totals in its currency."]),
            session,
            authorizationService,
            InvoicingConstants.CollectionName,
            InvoicingPermissions.ViewInvoices)
    {
        var S = stringLocalizer;
        var creditNote = S["Credit note"].Value;
        var money = S["Money"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the credit note's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Credit note ID"], DataFieldType.Text, record => record.ItemId, creditNote).Identifier();
        AddField(nameof(CreditNote.Number), S["Number"], DataFieldType.Text, record => record.Number, creditNote);
        AddField(nameof(CreditNote.Status), S["Status"], DataFieldType.Text, record => record.Status, creditNote);
        AddField(nameof(CreditNote.Disposition), S["Given back as"], DataFieldType.Text, record => record.Disposition, creditNote).Description = S["ReduceBalance, Refund or CustomerCredit."].Value;
        AddField(nameof(CreditNote.Reason), S["Reason"], DataFieldType.Text, record => record.Reason, creditNote);
        AddField(nameof(CreditNote.InvoiceId), S["Invoice ID"], DataFieldType.Text, record => record.InvoiceId, creditNote)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InvoicingSource.Name, CommerceReportDataSets.InvoicingSource.Invoices));
        AddField(nameof(CreditNote.InvoiceNumber), S["Invoice number"], DataFieldType.Text, record => record.InvoiceNumber, creditNote);
        AddField("BillToId", S["Bill-to ID"], DataFieldType.Text, record => InvoicingReportFields.Party(record).Id, creditNote).Identifier();
        AddField("BillToName", S["Bill-to name"], DataFieldType.Text, record => record.BillTo?.Contact?.Name, creditNote);

        AddField(nameof(CreditNote.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, money);
        AddField("Subtotal", S["Subtotal"], DataFieldType.Decimal, record => record.Totals?.Subtotal ?? 0m, money).Description = inCurrency;
        AddField("TaxTotal", S["Tax"], DataFieldType.Decimal, record => record.Totals?.TaxTotal ?? 0m, money).Description = inCurrency;
        AddField("Total", S["Total"], DataFieldType.Decimal, record => record.Totals?.Total ?? 0m, money).Description = inCurrency;
        AddField(nameof(CreditNote.AmountApplied), S["Taken off the balance"], DataFieldType.Decimal, record => record.AmountApplied, money).Description = inCurrency;
        AddField(nameof(CreditNote.AmountRefunded), S["Refunded"], DataFieldType.Decimal, record => record.AmountRefunded, money).Description = inCurrency;
        AddField(nameof(CreditNote.AmountCredited), S["Kept as credit"], DataFieldType.Decimal, record => record.AmountCredited, money).Description = inCurrency;

        AddField("IssueDate", S["Issue date"], DataFieldType.Date, record => InvoicingReportFields.ToDate(record.IssueDate), dates);
        AddField(InvoicingReportFields.CreatedUtcField, S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(CreditNote.IssuedUtc), S["Issued"], DataFieldType.DateTime, record => record.IssuedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CreditNoteIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CreditNoteIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(CreditNote.InvoiceId)] = index => index.InvoiceId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CreditNoteIndex> columns)
        => columns.Text(nameof(CreditNote.Currency), nameof(CreditNoteIndex.Currency));
}
