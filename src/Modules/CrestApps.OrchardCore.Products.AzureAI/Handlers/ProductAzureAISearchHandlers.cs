using CrestApps.OrchardCore.Products.AzureAI.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Services;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Products.AzureAI.Handlers;

/// <summary>
/// Declares the fields of an Azure AI Search product search index: those every product has and those of each
/// attribute. A new attribute is declared when product types change, which rebuilds the index.
/// </summary>
public sealed class ProductAzureAISearchIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductAzureAISearchIndexProfileHandler(ProductAttributeCatalog attributeCatalog)
    {
        _attributeCatalog = attributeCatalog;
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingsAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingsAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingsAsync(context.Model);

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

    private async Task SetMappingsAsync(IndexProfile indexProfile)
    {
        if (!IsProductSearch(indexProfile))
        {
            return;
        }

        var attributeKeys = (await _attributeCatalog.GetAttributesAsync([], includeUnused: true)).Select(attribute => attribute.Key);
        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();

        metadata.IndexMappings.Clear();

        foreach (var map in AzureAIProductSearchFields.GetMappings(attributeKeys))
        {
            metadata.IndexMappings.Add(map);
        }

        indexProfile.Put(metadata);
    }

    internal static bool IsProductSearch(IndexProfile indexProfile)
        => string.Equals(indexProfile?.ProviderName, AzureAISearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(indexProfile.Type, ProductSearchIndexFields.IndexingTaskType, StringComparison.OrdinalIgnoreCase);
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
            !ProductAzureAISearchIndexProfileHandler.IsProductSearch(profile as IndexProfile))
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
