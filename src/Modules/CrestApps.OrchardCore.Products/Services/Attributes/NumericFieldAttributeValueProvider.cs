using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a numeric field, such as a year, a mileage or a number of bedrooms.
/// </summary>
public sealed class NumericFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "NumericField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Number;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var number = FieldValueReader.GetDecimal(context.Value, "Value");

        if (!number.HasValue)
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = FieldValueReader.ToInvariant(number.Value),
                Number = number.Value,
            },
        ];

        return ValueTask.FromResult(values);
    }
}
