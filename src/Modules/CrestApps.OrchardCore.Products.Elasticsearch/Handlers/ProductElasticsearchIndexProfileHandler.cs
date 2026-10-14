using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Services;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Products.Elasticsearch.Handlers;

/// <summary>
/// Writes the mapping of an Elasticsearch product search index: the fields every product has and those of each
/// attribute, with dynamic templates so an attribute added before the next rebuild is still mapped by kind.
/// </summary>
public sealed class ProductElasticsearchIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductElasticsearchIndexProfileHandler(ProductAttributeCatalog attributeCatalog)
    {
        _attributeCatalog = attributeCatalog;
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(indexProfile.Type, ProductSearchIndexFields.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
