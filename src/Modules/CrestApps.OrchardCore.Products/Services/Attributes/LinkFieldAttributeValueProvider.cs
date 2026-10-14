using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// A link, such as a manufacturer's page. Its text is what visitors see and filter by, and its address is kept
/// with it.
/// </summary>
public sealed class LinkFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "LinkField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Text;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var url = FieldValueReader.GetString(context.Value, "Url");
        var text = FieldValueReader.GetString(context.Value, "Text");

        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(text))
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        var label = string.IsNullOrWhiteSpace(text) ? url : text.Trim();

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = label,
                Label = label,
                Url = url,
                Searchable = context.Settings.Searchable,
            },
        ];

        return ValueTask.FromResult(values);
    }
}
