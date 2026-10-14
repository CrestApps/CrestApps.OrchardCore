namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Field-level settings that make a content field a product attribute: something visitors can filter or sort
/// by, or see on a product card and in the specification table.
/// </summary>
/// <remarks>
/// A product type is an ordinary content type, so its attributes are its own fields: a vehicle's year and
/// mileage, a house's bedrooms. These settings only say which of them matter to the listing, which keeps the
/// typed editors, validation and localization that Orchard already gives every field.
/// </remarks>
public sealed class ProductAttributeSettings
{
    /// <summary>
    /// Gets or sets whether the field can be offered as a filter on a listing page.
    /// </summary>
    public bool Filterable { get; set; }

    /// <summary>
    /// Gets or sets whether the field can be offered as a sort option. Only numeric and date fields sort.
    /// </summary>
    public bool Sortable { get; set; }

    /// <summary>
    /// Gets or sets whether the field's text is matched by keyword search.
    /// </summary>
    public bool Searchable { get; set; }

    /// <summary>
    /// Gets or sets whether the value is shown as a short chip on the product card.
    /// </summary>
    public bool ShowOnCard { get; set; }

    /// <summary>
    /// Gets or sets whether the value is shown in the specification table on list rows and the product page.
    /// </summary>
    public bool ShowInSpecifications { get; set; }

    /// <summary>
    /// Gets or sets the label shown to visitors. The field's display name is used when it is empty.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the .NET format string for numbers, such as <c>0</c> for a year or <c>#,0</c> for a
    /// mileage. Numbers are shown with thousands separators when it is empty.
    /// </summary>
    public string NumberFormat { get; set; }

    /// <summary>
    /// Gets or sets text shown after a number, such as <c>mi</c> or <c>sq ft</c>.
    /// </summary>
    public string Suffix { get; set; }

    /// <summary>
    /// Gets or sets the colors of the field's values when a filter draws them as swatches, keyed by value name,
    /// such as <c>Midnight</c> to <c>#191970</c>. A value with no color here uses its own name when that is a
    /// CSS color.
    /// </summary>
    public Dictionary<string, string> SwatchColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets, for a taxonomy field, the name of a text field on the terms that holds each term's swatch
    /// color, so a color is set once on its term.
    /// </summary>
    public string TermColorField { get; set; }

    /// <summary>
    /// Gets a value indicating whether the field takes part in the listing at all.
    /// </summary>
    public bool IsUsed => Filterable || Sortable || Searchable || ShowOnCard || ShowInSpecifications;
}
