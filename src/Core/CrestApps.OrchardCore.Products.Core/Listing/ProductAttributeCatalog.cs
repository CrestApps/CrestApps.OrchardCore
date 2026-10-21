using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Answers which content types are products and which of their fields are product attributes.
/// </summary>
public sealed class ProductAttributeCatalog
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IProductAttributeValueProvider> _providers;

    public ProductAttributeCatalog(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IProductAttributeValueProvider> providers)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _providers = providers;
    }

    /// <summary>
    /// Gets every content type that carries a product part.
    /// </summary>
    public async Task<IReadOnlyList<ContentTypeDefinition>> GetProductTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(IsProductType)
            .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Whether the content type carries a product part.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static bool IsProductType(ContentTypeDefinition definition)
        => definition?.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)) == true;

    /// <summary>
    /// Gets the product part settings of a content type.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    public static ProductPartSettings GetProductSettings(ContentTypeDefinition definition)
        => definition?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart))?
            .GetSettings<ProductPartSettings>();

    /// <summary>
    /// Gets the attributes of the supplied content types, in field order. A field name shared by several
    /// types is one attribute, so a filter on "Year" works across vehicles and motorcycles.
    /// </summary>
    /// <param name="contentTypes">The content types, or every product type when empty.</param>
    /// <param name="includeUnused">Whether to include supported fields that are not marked as attributes.</param>
    public async Task<IReadOnlyList<ProductAttributeDescriptor>> GetAttributesAsync(IEnumerable<string> contentTypes, bool includeUnused = false)
    {
        var types = await ResolveTypesAsync(contentTypes);
        var attributes = new List<ProductAttributeDescriptor>();
        var byKey = new Dictionary<string, ProductAttributeDescriptor>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var field in GetFields(type))
            {
                var settings = field.Field.GetSettings<ProductAttributeSettings>();

                if (!includeUnused && !settings.IsUsed)
                {
                    continue;
                }

                if (byKey.TryGetValue(field.Field.Name, out var existing))
                {
                    existing.ContentTypes.Add(type.Name);

                    continue;
                }

                var descriptor = new ProductAttributeDescriptor
                {
                    Key = field.Field.Name,
                    Label = string.IsNullOrWhiteSpace(settings.Label) ? field.Field.DisplayName() : settings.Label,
                    FieldType = field.Field.FieldDefinition.Name,
                    Kind = field.Provider.GetValueKind(field.Field),
                    Settings = settings,
                    Order = attributes.Count,
                    TaxonomyContentItemId = field.Field.FieldDefinition.Name == "TaxonomyField"
                        ? FieldValueReader.GetSettingString(field.Field, "TaxonomyFieldSettings", "TaxonomyContentItemId")
                        : null,
                };

                descriptor.ContentTypes.Add(type.Name);
                byKey[descriptor.Key] = descriptor;
                attributes.Add(descriptor);
            }

            // A type sold in variants filters on its options as if they were fields.
            foreach (var option in GetVariantOptions(type))
            {
                var key = ProductVariants.AttributeKey(option.Name);

                if (!includeUnused && !option.Filterable)
                {
                    continue;
                }

                if (byKey.TryGetValue(key, out var existing))
                {
                    existing.ContentTypes.Add(type.Name);

                    continue;
                }

                var descriptor = new ProductAttributeDescriptor
                {
                    Key = key,
                    Label = option.Name,
                    FieldType = VariantOptionFieldType,
                    Kind = ProductAttributeValueKind.Text,
                    Settings = new ProductAttributeSettings { Filterable = option.Filterable, Label = option.Name },
                    Order = attributes.Count,
                };

                descriptor.ContentTypes.Add(type.Name);
                byKey[key] = descriptor;
                attributes.Add(descriptor);
            }
        }

        return attributes;
    }

    /// <summary>
    /// The field type of an attribute made from a variant option.
    /// </summary>
    public const string VariantOptionFieldType = "ProductVariantOption";

    /// <summary>
    /// The options of a type sold in variants, from its Product variants part settings.
    /// </summary>
    public static IEnumerable<ProductVariantOptionSettings> GetVariantOptions(ContentTypeDefinition type)
        => type?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductVariantsPart))?
            .GetSettings<ProductVariantsPartSettings>()?
            .Options?
            .Where(option => !string.IsNullOrWhiteSpace(option?.Name)) ?? [];

    /// <summary>
    /// Gets the fields of a content type that a provider can read, with the part they belong to.
    /// </summary>
    /// <param name="type">The content type definition.</param>
    public IEnumerable<ProductAttributeField> GetFields(ContentTypeDefinition type)
    {
        foreach (var part in type.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                var provider = _providers.FirstOrDefault(candidate => candidate.CanHandle(field.FieldDefinition.Name));

                if (provider is not null)
                {
                    yield return new ProductAttributeField(part, field, provider);
                }
            }
        }
    }

    private async Task<IReadOnlyList<ContentTypeDefinition>> ResolveTypesAsync(IEnumerable<string> contentTypes)
    {
        var requested = contentTypes?.Where(name => !string.IsNullOrEmpty(name)).ToArray() ?? [];
        var productTypes = await GetProductTypesAsync();

        return requested.Length == 0
            ? productTypes
            : productTypes.Where(type => requested.Contains(type.Name, StringComparer.Ordinal)).ToArray();
    }
}
