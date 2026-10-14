using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a multi-value text field, producing one value per selected option.
/// </summary>
public sealed class MultiTextFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "MultiTextField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Text;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var options = FieldValueReader.GetOptions(context.Field, "MultiTextFieldSettings");
        var values = new List<ProductAttributeValue>();

        foreach (var text in FieldValueReader.GetStrings(context.Value, "Values").Distinct(StringComparer.Ordinal))
        {
            var index = options.ToList().FindIndex(option => string.Equals(option.Value, text, StringComparison.Ordinal));

            values.Add(new ProductAttributeValue
            {
                Key = context.Key,
                Text = text,
                Label = index >= 0 ? options[index].Name : text,
                Order = index >= 0 ? index : int.MaxValue,
                Searchable = context.Settings.Searchable,
            });
        }

        return ValueTask.FromResult<IEnumerable<ProductAttributeValue>>(values);
    }
}
