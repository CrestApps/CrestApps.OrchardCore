using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

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
            model.SourceIndexProfileName = dataSource.SourceIndexProfileName;
            model.AIKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName;
            model.KeyFieldName = dataSource.KeyFieldName;
            model.TitleFieldName = dataSource.TitleFieldName;
            model.ContentFieldName = dataSource.ContentFieldName;

            // Lock configuration once both index and master index are set (already created),
            // but allow editing if either is missing (e.g., migration failure).
            model.IsLocked = !string.IsNullOrEmpty(dataSource.SourceIndexProfileName) &&
                !string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) &&
                !string.IsNullOrEmpty(dataSource.ContentFieldName) &&
                !string.IsNullOrEmpty(dataSource.ItemId);

            // Show ALL source indexes from all providers, excluding master indexes.
            var allIndexes = await _indexProfileStore.GetAllAsync();

            model.SourceIndexProfileNames = allIndexes
                .Where(i => !string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.ProviderName)
                .OrderBy(g => g.Key)
                .SelectMany(g =>
                {
                    var group = new SelectListGroup { Name = g.Key };
                    return g.OrderBy(i => i.Name).Select(i => new SelectListItem(i.Name, i.Name) { Group = group });
                });

            // Show ALL master indexes from all providers, grouped by provider.
            model.AIKnowledgeBaseIndexProfileNames = allIndexes
                .Where(i => string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.ProviderName)
                .OrderBy(g => g.Key)
                .SelectMany(g =>
                {
                    var group = new SelectListGroup { Name = g.Key };
                    return g.OrderBy(i => i.Name).Select(i => new SelectListItem(i.Name, i.Name) { Group = group });
                });

            // Build field names from the selected source index profile.
            if (!string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
            {
                var sourceProfile = allIndexes.FirstOrDefault(i =>
                    string.Equals(i.Name, dataSource.SourceIndexProfileName, StringComparison.OrdinalIgnoreCase));

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
        // Allow updating if new OR if fields are missing (migration failure recovery).
        var canUpdate = context.IsNew ||
            string.IsNullOrEmpty(dataSource.SourceIndexProfileName) ||
            string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName);

        if (!canUpdate)
        {
            return Edit(dataSource, context);
        }

        var model = new EditAIDataSourceIndexViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        dataSource.SourceIndexProfileName = model.SourceIndexProfileName;
        dataSource.AIKnowledgeBaseIndexProfileName = model.AIKnowledgeBaseIndexProfileName;
        dataSource.KeyFieldName = model.KeyFieldName;
        dataSource.TitleFieldName = model.TitleFieldName;
        dataSource.ContentFieldName = model.ContentFieldName;

        return Edit(dataSource, context);
    }

    private static IEnumerable<SelectListItem> GetFieldNamesFromProfile(IndexProfile profile)
    {
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
                if (indexMappings is System.Text.Json.Nodes.JsonArray array)
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
