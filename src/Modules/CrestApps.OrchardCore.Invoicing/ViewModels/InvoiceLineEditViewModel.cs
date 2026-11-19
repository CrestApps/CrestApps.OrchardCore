using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// One row of the draft invoice editor.
/// </summary>
public class InvoiceLineEditViewModel
{
    public string Id { get; set; }

    public DocumentLineKind Kind { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal? Quantity { get; set; }

    public string Unit { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? DiscountPercent { get; set; }

    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the product the line was picked from, or <see langword="null"/>.
    /// </summary>
    public string ProductId { get; set; }

    public string Sku { get; set; }

    public bool Remove { get; set; }

    /// <summary>
    /// Gets or sets the row's tax from the last calculation, shown only.
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the row's effective tax rate in percent from the last calculation, which the editor uses to estimate
    /// the tax while it is changed; <see langword="null"/> until it has been worked out.
    /// </summary>
    public decimal? TaxRate { get; set; }

    /// <summary>
    /// Gets or sets the row's amount from the last calculation, shown only.
    /// </summary>
    public decimal? Total { get; set; }
}
