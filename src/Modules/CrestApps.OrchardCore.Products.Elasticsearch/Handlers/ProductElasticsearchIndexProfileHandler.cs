using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Products.Elasticsearch.Handlers;

/// <summary>
/// Writes the mapping of an Elasticsearch product search index: the fields every product has and those of each
/// attribute, with dynamic templates so an attribute added before the next rebuild is still mapped by kind.
/// </summary>
public sealed class ProductElasticsearchIndexProfileHandler : ProductSearchIndexProfileHandlerBase
{
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductElasticsearchIndexProfileHandler(ProductAttributeCatalog attributeCatalog)
        : base(ElasticsearchConstants.ProviderName)
    {
        _attributeCatalog = attributeCatalog;
    }

    /// <inheritdoc/>
    protected override async Task ConfigureAsync(IndexProfile indexProfile)
    {
        var attributeKeys = (await _attributeCatalog.GetAttributesAsync([], includeUnused: true)).Select(attribute => attribute.Key);
        var metadata = indexProfile.GetOrCreate<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.KeyFieldName = ProductSearchIndexFields.ContentItemId;

        var properties = new Properties();

        foreach (var definition in ProductSearchIndexFields.GetDefinitions(attributeKeys))
        {
            properties[definition.Name] = ToProperty(definition.Kind);
        }

        metadata.IndexMappings.Mapping = new TypeMapping
        {
            Properties = properties,
            DynamicTemplates =
            [
                Template("product_values", ProductSearchIndexFields.ValuesSuffix, ProductIndexFieldKind.Keyword),
                Template("product_facets", ProductSearchIndexFields.FacetsSuffix, ProductIndexFieldKind.Keyword),
                Template("product_sort_texts", ProductSearchIndexFields.SortTextSuffix, ProductIndexFieldKind.Keyword),
                Template("product_numbers", ProductSearchIndexFields.NumbersSuffix, ProductIndexFieldKind.Number),
                Template("product_sort_numbers", ProductSearchIndexFields.SortNumberSuffix, ProductIndexFieldKind.Number),
                Template("product_max_numbers", ProductSearchIndexFields.MaxNumberSuffix, ProductIndexFieldKind.Number),
            ],
        };

        indexProfile.Put(metadata);

        var queryMetadata = indexProfile.GetOrCreate<ElasticsearchDefaultQueryMetadata>();

        if (queryMetadata.DefaultSearchFields is null || queryMetadata.DefaultSearchFields.Length == 0)
        {
            queryMetadata.DefaultSearchFields = [ProductSearchIndexFields.Title, ProductSearchIndexFields.Keywords];
            indexProfile.Put(queryMetadata);
        }
    }

    private static KeyValuePair<string, DynamicTemplate> Template(string name, string suffix, ProductIndexFieldKind kind)
    {
        DynamicTemplate template = kind == ProductIndexFieldKind.Number ? new DoubleNumberProperty() : new KeywordProperty();
        template.Match = [$"a_*{suffix}"];

        return new(name, template);
    }

    private static IProperty ToProperty(ProductIndexFieldKind kind)
        => kind switch
        {
            ProductIndexFieldKind.Text => new TextProperty(),
            ProductIndexFieldKind.Number => new DoubleNumberProperty(),
            ProductIndexFieldKind.Date => new DateProperty(),
            _ => new KeywordProperty(),
        };
}
