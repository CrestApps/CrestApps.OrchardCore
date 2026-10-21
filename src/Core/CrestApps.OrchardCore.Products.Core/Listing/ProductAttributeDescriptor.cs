namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// One product attribute as the listing sees it.
/// </summary>
public sealed class ProductAttributeDescriptor
{
    public string Key { get; set; }

    public string Label { get; set; }

    public string FieldType { get; set; }

    public ProductAttributeValueKind Kind { get; set; }

    public ProductAttributeSettings Settings { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// The taxonomy a taxonomy field picks its terms from, or <see langword="null"/> for any other field.
    /// </summary>
    public string TaxonomyContentItemId { get; set; }

    public List<string> ContentTypes { get; } = [];
}
