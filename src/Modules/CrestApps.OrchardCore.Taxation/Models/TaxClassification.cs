namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a tax classification resolved for a content item. A classification carries the tax
/// category and, optionally, a refining classification code and an external provider code. It never
/// carries a rate; the taxation engine determines the applicable tax from this classification.
/// </summary>
public sealed class TaxClassification
{
    /// <summary>
    /// Gets or sets the tax category code (for example <c>Electronics</c>).
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the tax classification code that refines the category.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets an optional external or provider-specific tax code.
    /// </summary>
    public string ExternalTaxCode { get; set; }
}
