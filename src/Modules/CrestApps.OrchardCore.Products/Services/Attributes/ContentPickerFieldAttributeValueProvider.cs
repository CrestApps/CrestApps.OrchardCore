using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a content picker field, such as the agent or the dealer location a product belongs to. The linked
/// item's title becomes the label.
/// </summary>
public sealed class ContentPickerFieldAttributeValueProvider : IProductAttributeValueProvider
{
    private readonly IContentManager _contentManager;

    public ContentPickerFieldAttributeValueProvider(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "ContentPickerField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Text;

    public async ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var ids = FieldValueReader.GetStrings(context.Value, "ContentItemIds").Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = await _contentManager.GetAsync(ids, VersionOptions.Published);

        return items
            .Where(item => item is not null)
            .Select(item => new ProductAttributeValue
            {
                Key = context.Key,
                Text = item.ContentItemId,
                Label = string.IsNullOrEmpty(item.DisplayText) ? item.ContentItemId : item.DisplayText,
                Order = int.MaxValue,
                Searchable = context.Settings.Searchable,
            })
            .ToArray();
    }
}
