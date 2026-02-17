using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;

    internal readonly IStringLocalizer S;

    public AIDataSourceDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IStringLocalizer<AIDataSourceDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIDataSource dataSource, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIDataSource_Fields_SummaryAdmin", dataSource).Location("Content:1"),
            View("AIDataSource_Buttons_SummaryAdmin", dataSource).Location("Actions:5"),
            View("AIDataSource_DefaultTags_SummaryAdmin", dataSource).Location("Tags:5"),
            View("AIDataSource_DefaultMeta_SummaryAdmin", dataSource).Location("Meta:5"),
            View("AIDataSource_ActionsMenu_SummaryAdmin", dataSource).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        return Initialize<EditAIDataSourceViewModel>("AIDataSourceFields_Edit", async model =>
        {
            model.DisplayText = dataSource.DisplayText;
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
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        var model = new EditAIDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required field."]);
        }

        dataSource.DisplayText = model.DisplayText;

        // Allow updating index config if new OR if fields are missing (migration failure recovery).
        var canUpdateIndex = context.IsNew ||
            string.IsNullOrEmpty(dataSource.SourceIndexProfileName) ||
            string.IsNullOrEmpty(dataSource.ContentFieldName) ||
            string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName);

        if (canUpdateIndex)
        {
            dataSource.SourceIndexProfileName = model.SourceIndexProfileName;
            dataSource.AIKnowledgeBaseIndexProfileName = model.AIKnowledgeBaseIndexProfileName;
            dataSource.KeyFieldName = model.KeyFieldName;
            dataSource.TitleFieldName = model.TitleFieldName;
            dataSource.ContentFieldName = model.ContentFieldName;
        }

        return Edit(dataSource, context);
    }
}
