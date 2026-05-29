using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IndexingOptions _indexingOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="indexingOptions">The indexing options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourceDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IOptions<IndexingOptions> indexingOptions,
        IStringLocalizer<AIDataSourceDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        _indexingOptions = indexingOptions.Value;
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

            // Show source indexes from all providers, excluding AI-managed index profiles.
            var allIndexes = await _indexProfileStore.GetAllAsync();

            var sourceIndexes = allIndexes
                .Where(i =>
                    !string.Equals(i.Type, AIConstants.AIDocumentsIndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(i.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase));
            model.SourceIndexProfileNames = BuildGroupedIndexProfileItems(sourceIndexes, _indexingOptions);

            // Show ALL master indexes from all providers, grouped by provider.
            var knowledgeBaseIndexes = allIndexes
                .Where(i => string.Equals(i.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase));
            model.AIKnowledgeBaseIndexProfileNames = BuildGroupedIndexProfileItems(knowledgeBaseIndexes, _indexingOptions);

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

    private static IEnumerable<SelectListItem> BuildGroupedIndexProfileItems(
        IEnumerable<IndexProfile> indexProfiles,
        IndexingOptions indexingOptions)
    {
        return indexProfiles
            .GroupBy(profile => profile.ProviderName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetProviderDisplayName(group.Key, indexingOptions), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var selectListGroup = new SelectListGroup
                {
                    Name = GetProviderDisplayName(group.Key, indexingOptions),
                };

                return group
                    .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(profile => new SelectListItem(profile.Name, profile.Name)
                    {
                        Group = selectListGroup,
                    });
            });
    }

    private static string GetProviderDisplayName(string providerName, IndexingOptions indexingOptions)
    {
        if (!string.IsNullOrWhiteSpace(providerName) &&
            indexingOptions?.Providers.TryGetValue(providerName, out var entry) == true &&
            !string.IsNullOrWhiteSpace(entry.DisplayName.Value))
        {
            return entry.DisplayName.Value;
        }

        return providerName ?? string.Empty;
    }
}
