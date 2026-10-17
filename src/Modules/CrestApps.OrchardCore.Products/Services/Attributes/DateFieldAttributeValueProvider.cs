using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a date or date-and-time field, such as an availability date.
/// </summary>
public sealed class DateFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName is "DateField" or "DateTimeField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Date;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var date = FieldValueReader.GetDate(context.Value, "Value");

        if (!date.HasValue)
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = date.Value.ToString("O", CultureInfo.InvariantCulture),
                Date = date.Value,

                // As a number too, so date filters reuse the number ranges.
                Number = ProductDateNumbers.ToNumber(date.Value),
            },
        ];

        return ValueTask.FromResult(values);
    }
}
