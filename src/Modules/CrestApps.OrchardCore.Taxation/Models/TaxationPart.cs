using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A content part that classifies a content item for taxation. The part carries tax identity and
/// classification only; it never stores a final tax rate. The taxation engine determines the applicable
/// tax from the transaction context using this classification.
/// </summary>
public sealed class TaxationPart : ContentPart
{
    /// <summary>
    /// Gets or sets a value indicating whether the content item is taxable.
    /// </summary>
    public bool Taxable { get; set; } = true;

    /// <summary>
    /// Gets or sets the tax category code (for example <c>Electronics</c>).
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the tax classification code (for example <c>Television</c>) that refines the category.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets an optional external or provider-specific tax code.
    /// </summary>
    public string ExternalTaxCode { get; set; }
}
