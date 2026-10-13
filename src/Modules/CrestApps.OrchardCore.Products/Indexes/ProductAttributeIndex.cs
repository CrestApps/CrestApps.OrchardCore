using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

/// <summary>
/// One row per attribute value of a listed product. Filters match these rows and facets count them.
/// </summary>
public sealed class ProductAttributeIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string AttributeKey { get; set; }

    public string TextValue { get; set; }

    public decimal? NumberValue { get; set; }

    public DateTime? DateValue { get; set; }

    public string Label { get; set; }

    public int Level { get; set; }

    public int SortOrder { get; set; }

    public bool Searchable { get; set; }
}
