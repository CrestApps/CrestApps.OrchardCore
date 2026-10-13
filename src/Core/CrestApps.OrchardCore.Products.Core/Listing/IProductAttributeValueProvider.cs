using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Turns one kind of content field into product attribute values.
/// </summary>
/// <remarks>
/// One provider exists per field type. Adding a provider is how a custom field becomes filterable.
/// </remarks>
public interface IProductAttributeValueProvider
{
    /// <summary>
    /// Whether the provider reads fields of this type.
    /// </summary>
    /// <param name="fieldTypeName">The field type name, such as <c>TextField</c>.</param>
    bool CanHandle(string fieldTypeName);

    /// <summary>
    /// Describes what kind of values the field produces, so the listing can offer the right filter.
    /// </summary>
    /// <param name="field">The field definition.</param>
    ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field);

    /// <summary>
    /// Reads the field's values from a product.
    /// </summary>
    /// <param name="context">The field, its stored value and the product it belongs to.</param>
    ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context);
}

/// <summary>
/// The kind of values an attribute holds.
/// </summary>
public enum ProductAttributeValueKind
{
    /// <summary>
    /// Text options, such as a color.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Numbers, such as a year or a mileage.
    /// </summary>
    Number = 1,

    /// <summary>
    /// Dates.
    /// </summary>
    Date = 2,

    /// <summary>
    /// Yes or no.
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// Taxonomy terms, which can be nested.
    /// </summary>
    Term = 4,
}

/// <summary>
/// The input to <see cref="IProductAttributeValueProvider.GetValuesAsync"/>.
/// </summary>
public sealed class ProductAttributeValueContext
{
    /// <summary>
    /// Gets the product being read.
    /// </summary>
    public required ContentItem ContentItem { get; init; }

    /// <summary>
    /// Gets the field definition.
    /// </summary>
    public required ContentPartFieldDefinition Field { get; init; }

    /// <summary>
    /// Gets the field's stored value, or <see langword="null"/> when the product has none.
    /// </summary>
    public JsonObject Value { get; init; }

    /// <summary>
    /// Gets the attribute key the values must carry.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the field's attribute settings.
    /// </summary>
    public required ProductAttributeSettings Settings { get; init; }
}
