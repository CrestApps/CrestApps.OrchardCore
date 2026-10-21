using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// The users picked on a product, such as its agent or seller. Each user is an option, filtered by identifier and
/// shown by name.
/// </summary>
public sealed class UserPickerFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "UserPickerField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Text;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var ids = FieldValueReader.GetStrings(context.Value, "UserIds").ToArray();

        // The field keeps the user names beside the identifiers; a value saved without them, such as an imported one, shows the identifier.
        var names = FieldValueReader.GetStrings(context.Value, "UserNames").ToArray();

        IEnumerable<ProductAttributeValue> values = ids
            .Select((id, index) => new ProductAttributeValue
            {
                Key = context.Key,
                Text = id,
                Label = index < names.Length && !string.IsNullOrWhiteSpace(names[index]) ? names[index] : id,
                Searchable = context.Settings.Searchable,
            })
            .Where(value => !string.IsNullOrEmpty(value.Text))
            .ToArray();

        return ValueTask.FromResult(values);
    }
}
