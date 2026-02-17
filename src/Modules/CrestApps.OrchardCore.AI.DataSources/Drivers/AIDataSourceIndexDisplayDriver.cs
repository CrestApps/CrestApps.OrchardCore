using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;

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
                !string.IsNullOrEmpty(dataSource.ContentFieldName);

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
}
