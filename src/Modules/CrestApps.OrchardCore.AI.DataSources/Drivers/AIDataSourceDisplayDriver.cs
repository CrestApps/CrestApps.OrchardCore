using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
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
    private readonly AIDataSourceSourceOptions _sourceOptions;
    private readonly IndexingOptions _indexingOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="sourceOptions">The source options.</param>
    /// <param name="indexingOptions">The indexing options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDataSourceDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IOptions<AIDataSourceSourceOptions> sourceOptions,
        IOptions<IndexingOptions> indexingOptions,
        IStringLocalizer<AIDataSourceDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        _sourceOptions = sourceOptions.Value;
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
            model.SourceType = AIDataSourceDriverHelper.GetSourceType(dataSource);
            model.AIKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName;
            model.IsConfigurationLocked = AIDataSourceDriverHelper.IsConfigurationLocked(dataSource);

            var allIndexes = await _indexProfileStore.GetAllAsync();
            var knowledgeBaseIndexes = allIndexes
                .Where(index => string.Equals(index.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase));
            model.AIKnowledgeBaseIndexProfileNames = BuildGroupedIndexProfileItems(knowledgeBaseIndexes);
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

        if (!AIDataSourceDriverHelper.IsConfigurationLocked(dataSource))
        {
            dataSource.SourceType = string.IsNullOrWhiteSpace(model.SourceType)
                ? AIDataSourceSourceTypes.SearchIndexProfile
                : model.SourceType.Trim();
            dataSource.AIKnowledgeBaseIndexProfileName = model.AIKnowledgeBaseIndexProfileName;
        }

        return Edit(dataSource, context);
    }

    private IEnumerable<SelectListItem> BuildGroupedIndexProfileItems(IEnumerable<IndexProfile> indexProfiles)
    {
        return indexProfiles
            .GroupBy(profile => profile.ProviderName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetProviderDisplayName(group.Key), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var selectListGroup = new SelectListGroup
                {
                    Name = GetProviderDisplayName(group.Key),
                };

                return group
                    .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(profile => new SelectListItem(profile.Name, profile.Name)
                    {
                        Group = selectListGroup,
                    });
            });
    }

    private string GetProviderDisplayName(string providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName) &&
            _indexingOptions.Providers.TryGetValue(providerName, out var entry) &&
            !string.IsNullOrWhiteSpace(entry.DisplayName.Value))
        {
            return entry.DisplayName.Value;
        }

        return providerName ?? string.Empty;
    }
}
