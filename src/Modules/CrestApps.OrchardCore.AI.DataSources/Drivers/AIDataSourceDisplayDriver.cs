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
        return Combine(
            Initialize<EditAIDataSourceFieldsViewModel>("AIDataSourceFields_Edit", model => PopulateFieldsEditorModelAsync(dataSource, model)).Location("Content:1"),
            Initialize<EditAIDataSourceSharedViewModel>("AIDataSourceShared_Edit", model => PopulateSharedEditorModelAsync(dataSource, model)).Location("Content:100")
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        var fieldsModel = new EditAIDataSourceFieldsViewModel();
        var sharedModel = new EditAIDataSourceSharedViewModel();

        await context.Updater.TryUpdateModelAsync(fieldsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(sharedModel, Prefix);

        if (string.IsNullOrEmpty(fieldsModel.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(fieldsModel.DisplayText), S["The name is required field."]);
        }

        if (string.IsNullOrWhiteSpace(sharedModel.AIKnowledgeBaseIndexProfileName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(sharedModel.AIKnowledgeBaseIndexProfileName), S["The destination index is required."]);
        }

        if (string.IsNullOrWhiteSpace(sharedModel.ContentFieldName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(sharedModel.ContentFieldName), S["The content field is required."]);
        }

        dataSource.DisplayText = fieldsModel.DisplayText;

        if (!AIDataSourceDriverHelper.IsConfigurationLocked(dataSource))
        {
            dataSource.AIKnowledgeBaseIndexProfileName = sharedModel.AIKnowledgeBaseIndexProfileName;
            dataSource.KeyFieldName = sharedModel.KeyFieldName;
            dataSource.TitleFieldName = sharedModel.TitleFieldName;
            dataSource.ContentFieldName = sharedModel.ContentFieldName;
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

    private static void PopulateBaseEditorModel(
        AIDataSource dataSource,
        out string sourceType,
        out bool isConfigurationLocked)
    {
        sourceType = AIDataSourceDriverHelper.GetSourceType(dataSource);
        isConfigurationLocked = AIDataSourceDriverHelper.IsConfigurationLocked(dataSource);
    }

    private static ValueTask PopulateFieldsEditorModelAsync(
        AIDataSource dataSource,
        EditAIDataSourceFieldsViewModel model)
    {
        PopulateBaseEditorModel(dataSource, out var sourceType, out var isConfigurationLocked);

        model.DisplayText = dataSource.DisplayText;
        model.SourceType = sourceType;
        model.IsConfigurationLocked = isConfigurationLocked;

        return ValueTask.CompletedTask;
    }

    private async ValueTask PopulateSharedEditorModelAsync(
        AIDataSource dataSource,
        EditAIDataSourceSharedViewModel model)
    {
        PopulateBaseEditorModel(dataSource, out var sourceType, out var isConfigurationLocked);

        model.SourceType = sourceType;
        model.AIKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName;
        model.KeyFieldName = dataSource.KeyFieldName;
        model.TitleFieldName = dataSource.TitleFieldName;
        model.ContentFieldName = dataSource.ContentFieldName;
        model.IsConfigurationLocked = isConfigurationLocked;

        var allIndexes = await _indexProfileStore.GetAllAsync();
        var knowledgeBaseIndexes = allIndexes
            .Where(index => string.Equals(index.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase));
        model.AIKnowledgeBaseIndexProfileNames = BuildGroupedIndexProfileItems(knowledgeBaseIndexes);
        model.FieldNames = [];
    }
}
