using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Reads a taxonomy field. Each selected term is stored with every one of its ancestors, so picking a parent
/// term (a make, a department) matches the products filed under any of its children (its models, its aisles).
/// </summary>
public sealed class TaxonomyFieldAttributeValueProvider : IProductAttributeValueProvider
{
    private readonly TaxonomyTermLookup _terms;

    public TaxonomyFieldAttributeValueProvider(TaxonomyTermLookup terms)
    {
        _terms = terms;
    }

    public bool CanHandle(string fieldTypeName)
        => fieldTypeName == "TaxonomyField";

    public ProductAttributeValueKind GetValueKind(ContentPartFieldDefinition field)
        => ProductAttributeValueKind.Term;

    public async ValueTask<IEnumerable<ProductAttributeValue>> GetValuesAsync(ProductAttributeValueContext context)
    {
        var taxonomyId = FieldValueReader.GetString(context.Value, "TaxonomyContentItemId")
            ?? FieldValueReader.GetSettingString(context.Field, "TaxonomyFieldSettings", "TaxonomyContentItemId");

        var values = new Dictionary<string, ProductAttributeValue>(StringComparer.Ordinal);

        foreach (var termId in FieldValueReader.GetStrings(context.Value, "TermContentItemIds"))
        {
            foreach (var term in await _terms.GetPathAsync(taxonomyId, termId))
            {
                values.TryAdd(term.ContentItemId, new ProductAttributeValue
                {
                    Key = context.Key,
                    Text = term.ContentItemId,
                    Label = term.DisplayText,
                    Level = term.Level,
                    Order = term.Order,
                    Searchable = context.Settings.Searchable,
                });
            }
        }

        return values.Values;
    }
}
