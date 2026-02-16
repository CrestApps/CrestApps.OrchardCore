using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIDataSourceIndexDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;

    public AIDataSourceIndexDisplayDriver(IIndexProfileStore indexProfileStore)
    {
        _indexProfileStore = indexProfileStore;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        return Initialize<EditAIDataSourceIndexViewModel>("AIDataSourceIndex_Edit", async model =>
        {
            var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();
            model.IndexName = indexMetadata.IndexName;
            model.MasterIndexName = indexMetadata.MasterIndexName;
            model.TitleFieldName = indexMetadata.TitleFieldName;
            model.ContentFieldName = indexMetadata.ContentFieldName;

            // Lock configuration once both index and master index are set (already created).
            model.IsLocked = !string.IsNullOrEmpty(indexMetadata.IndexName) &&
                             !string.IsNullOrEmpty(indexMetadata.MasterIndexName) &&
                             !string.IsNullOrEmpty(dataSource.ItemId);

            // Show ALL source indexes from all providers, excluding master indexes.
            var allIndexes = await _indexProfileStore.GetAllAsync();

            model.IndexNames = allIndexes
                .Where(i => !string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.ProviderName)
                .OrderBy(g => g.Key)
                .SelectMany(g =>
                {
                    var group = new SelectListGroup { Name = g.Key };
                    return g.OrderBy(i => i.Name).Select(i => new SelectListItem(i.Name, i.Name) { Group = group });
                });

            // Show ALL master indexes from all providers, grouped by provider.
            model.MasterIndexNames = allIndexes
                .Where(i => string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.ProviderName)
                .OrderBy(g => g.Key)
                .SelectMany(g =>
                {
                    var group = new SelectListGroup { Name = g.Key };
                    return g.OrderBy(i => i.Name).Select(i => new SelectListItem(i.Name, i.Name) { Group = group });
                });

            // Build field names from the selected source index profile.
            if (!string.IsNullOrEmpty(indexMetadata.IndexName))
            {
                var sourceProfile = allIndexes.FirstOrDefault(i =>
                    string.Equals(i.Name, indexMetadata.IndexName, StringComparison.OrdinalIgnoreCase));

                if (sourceProfile != null)
                {
                    model.FieldNames = GetFieldNamesFromProfile(sourceProfile);
                }
            }

            model.FieldNames ??= [];
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!context.IsNew)
        {
            return Edit(dataSource, context);
        }

        var model = new EditAIDataSourceIndexViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        dataSource.Put(new AIDataSourceIndexMetadata
        {
            IndexName = model.IndexName,
            MasterIndexName = model.MasterIndexName,
            TitleFieldName = model.TitleFieldName,
            ContentFieldName = model.ContentFieldName,
        });

        return Edit(dataSource, context);
    }

    private static IEnumerable<SelectListItem> GetFieldNamesFromProfile(IndexProfile profile)
    {
        // Extract field names from the index profile's metadata.
        // The exact metadata type depends on the provider, but we can use
        // common patterns to extract available fields.
        var fields = new List<SelectListItem>();

        if (profile.Properties != null)
        {
            // Try to extract fields from Elasticsearch mapping.
            var esMetadata = profile.Properties.TryGetPropertyValue("ElasticsearchIndexMetadata", out var esNode) ? esNode : null;
            if (esMetadata != null)
            {
                var mappings = esMetadata["IndexMappings"]?["Mapping"]?["Properties"];
                if (mappings != null)
                {
                    foreach (var prop in mappings.AsObject())
                    {
                        fields.Add(new SelectListItem(prop.Key, prop.Key));
                    }
                }
            }

            // Try to extract fields from Azure AI Search mapping.
            var azureMetadata = profile.Properties.TryGetPropertyValue("AzureAISearchIndexMetadata", out var azNode) ? azNode : null;
            if (azureMetadata != null)
            {
                var indexMappings = azureMetadata["IndexMappings"];
                if (indexMappings != null && indexMappings is System.Text.Json.Nodes.JsonArray array)
                {
                    foreach (var item in array)
                    {
                        var fieldKey = item?["AzureFieldKey"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(fieldKey))
                        {
                            fields.Add(new SelectListItem(fieldKey, fieldKey));
                        }
                    }
                }
            }
        }

        return fields.OrderBy(x => x.Text);
    }
}
