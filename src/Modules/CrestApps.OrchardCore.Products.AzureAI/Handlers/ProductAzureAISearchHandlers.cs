using CrestApps.OrchardCore.Products.AzureAI.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Products.AzureAI.Handlers;

/// <summary>
/// Declares the fields of an Azure AI Search product search index: those every product has and those of each
/// attribute. A new attribute is declared when product types change, which rebuilds the index.
/// </summary>
public sealed class ProductAzureAISearchIndexProfileHandler : ProductSearchIndexProfileHandlerBase
{
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductAzureAISearchIndexProfileHandler(ProductAttributeCatalog attributeCatalog)
        : base(AzureAISearchConstants.ProviderName)
    {
        _attributeCatalog = attributeCatalog;
    }

    // Orchard Core adds the posted fields to the saved ones when the profile is edited; keep one of each.
    public override Task LoadedAsync(LoadedContext<IndexProfile> context)
    {
        if (!IsProductSearch(context.Model))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.GetOrCreate<AzureAISearchIndexMetadata>();
        var distinct = metadata.IndexMappings
            .GroupBy(map => map.AzureFieldKey ?? map.IndexingKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (distinct.Count != metadata.IndexMappings.Count)
        {
            metadata.IndexMappings.Clear();

            foreach (var map in distinct)
            {
                metadata.IndexMappings.Add(map);
            }

            context.Model.Put(metadata);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task ConfigureAsync(IndexProfile indexProfile)
    {
        var attributeKeys = (await _attributeCatalog.GetAttributesAsync([], includeUnused: true)).Select(attribute => attribute.Key);
        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();

        metadata.IndexMappings.Clear();

        foreach (var map in AzureAIProductSearchFields.GetMappings(attributeKeys))
        {
            metadata.IndexMappings.Add(map);
        }

        indexProfile.Put(metadata);
    }
}

/// <summary>
/// Marks the sortable fields a product has, so products without a value sort last in Azure AI Search.
/// </summary>
public sealed class ProductAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ProductSearchDocument document ||
            !context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            !ProductSearchIndexFields.IsProductSearchIndex(profile as IndexProfile, AzureAISearchConstants.ProviderName))
        {
            return Task.CompletedTask;
        }

        foreach (var field in ProductSearchIndexFields.GetFields(document))
        {
            if (AzureAIProductSearchFields.IsSortable(field.Name))
            {
                context.DocumentIndex.Set(AzureAIProductSearchFields.HasValue(field.Name), (decimal?)1m, DocumentIndexOptions.None);
            }
        }

        return Task.CompletedTask;
    }
}
