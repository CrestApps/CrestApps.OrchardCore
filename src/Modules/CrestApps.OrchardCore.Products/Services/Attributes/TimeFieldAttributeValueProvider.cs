using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// A time of day, such as an event's start time. It is listed as <c>09:30</c> and sorts by minutes after midnight.
/// </summary>
public sealed class TimeFieldAttributeValueProvider : IProductAttributeValueProvider
{
    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "TimeField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Time;

    public ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var text = FieldValueReader.GetString(context.Value, "Value");

        if (string.IsNullOrWhiteSpace(text) || !TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time))
        {
            return ValueTask.FromResult(Enumerable.Empty<ProductAttributeValue>());
        }

        var display = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        IEnumerable<ProductAttributeValue> values =
        [
            new ProductAttributeValue
            {
                Key = context.Key,
                Text = display,
                Label = display,
                Number = (decimal)Math.Floor(time.TotalMinutes),
                Searchable = context.Settings.Searchable,
            },
        ];

        return ValueTask.FromResult(values);
    }
}
