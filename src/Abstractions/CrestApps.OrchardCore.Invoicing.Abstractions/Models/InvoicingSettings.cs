using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The site's invoicing settings: the defaults a new document starts from. The party's billing profile and the document
/// itself can change each of them; every effective value is copied onto the document when it is issued.
/// </summary>
public sealed class InvoicingSettings
{
    /// <summary>
    /// Gets or sets the id of the business document template new documents are printed with.
    /// </summary>
    public string DefaultTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the id of the payment term new invoices use when the party has none.
    /// </summary>
    public string DefaultPaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the currency of new documents when the party has none. Empty uses the store's currency.
    /// </summary>
    public string DefaultCurrency { get; set; }

    /// <summary>
    /// Gets or sets the notes new documents start with.
    /// </summary>
    public string DefaultNotes { get; set; }

    /// <summary>
    /// Gets or sets the terms and conditions new documents start with.
    /// </summary>
    public string DefaultTerms { get; set; }

    /// <summary>
    /// Gets or sets the footer new documents start with.
    /// </summary>
    public string DefaultFooter { get; set; }

    /// <summary>
    /// Gets or sets whether new documents are written with prices that include tax.
    /// </summary>
    public bool PricesIncludeTax { get; set; }

    /// <summary>
    /// Gets or sets how tax applies to a party that has no treatment of its own.
    /// </summary>
    public TaxTreatment DefaultTaxTreatment { get; set; }

    /// <summary>
    /// Gets or sets whether new invoices may be paid in part online.
    /// </summary>
    public bool AllowPartialPayment { get; set; }

    /// <summary>
    /// Gets or sets whether sending a draft issues it first.
    /// </summary>
    public bool IssueOnSend { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the PDF of a document is stored when it is issued.
    /// </summary>
    public bool ArchivePdfOnIssue { get; set; } = true;

    /// <summary>
    /// Gets or sets the smallest amount a total is rounded to for cash payment, such as <c>0.05</c>, or
    /// <see langword="null"/> for no cash rounding.
    /// </summary>
    public decimal? CashRoundingIncrement { get; set; }

    /// <summary>
    /// Gets or sets how many days a shared link to an invoice stays valid.
    /// </summary>
    public int ShareLinkDays { get; set; } = 60;
}
