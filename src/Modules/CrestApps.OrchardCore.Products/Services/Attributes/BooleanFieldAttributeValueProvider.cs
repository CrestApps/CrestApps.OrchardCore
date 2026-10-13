using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a yes/no field, such as "Featured" or "Pet friendly".
/// </summary>
public sealed class BooleanFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "BooleanField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Boolean;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var flag = FieldValueReader.GetBoolean(context.Value, "Value") ?? false;

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = flag ? "true" : "false",
            },
        ];

        return ValueTask.FromResult(values);
    }
}
