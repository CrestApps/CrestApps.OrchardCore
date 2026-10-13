using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a text field. When the field uses a predefined list, the option names become the labels and the
/// list order becomes the option order.
/// </summary>
public sealed class TextFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "TextField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Text;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var text = FieldValueReader.GetString(context.Value, "Text")?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        var options = FieldValueReader.GetOptions(context.Field, "TextFieldPredefinedListEditorSettings");
        var index = -1;

        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, text, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = text,
                Label = index >= 0 ? options[index].Name : text,
                Order = index >= 0 ? index : int.MaxValue,
                Searchable = context.Settings.Searchable,
            },
        ];

        return ValueTask.FromResult(values);
    }
}
