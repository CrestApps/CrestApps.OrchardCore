using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIDataSourceSearchIndexProfileDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IndexingOptions _indexingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceSearchIndexProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="indexingOptions">The indexing options.</param>
    public AIDataSourceSearchIndexProfileDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IOptions<IndexingOptions> indexingOptions)
    {
        _indexProfileStore = indexProfileStore;
        _indexingOptions = indexingOptions.Value;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceDriverHelper.GetSourceType(dataSource),
            AIDataSourceSourceTypes.SearchIndexProfile,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<EditAIDataSourceSearchIndexProfileViewModel>("AIDataSourceSearchIndexProfile_Edit", async model =>
        {
            model.SourceIndexProfileName = dataSource.SourceIndexProfileName;
            model.KeyFieldName = dataSource.KeyFieldName;
            model.TitleFieldName = dataSource.TitleFieldName;
            model.ContentFieldName = dataSource.ContentFieldName;
            model.IsConfigurationLocked = AIDataSourceDriverHelper.IsConfigurationLocked(dataSource);

            var allIndexes = await _indexProfileStore.GetAllAsync();
            var sourceIndexes = allIndexes
                .Where(index =>
                    !string.Equals(index.Type, AIConstants.AIDocumentsIndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(index.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(index.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase));
            model.SourceIndexProfileNames = BuildGroupedIndexProfileItems(sourceIndexes);
            model.FieldNames = [];
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (!string.Equals(
            AIDataSourceDriverHelper.GetSourceType(dataSource),
            AIDataSourceSourceTypes.SearchIndexProfile,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new EditAIDataSourceSearchIndexProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!AIDataSourceDriverHelper.IsConfigurationLocked(dataSource))
        {
            dataSource.SourceIndexProfileName = model.SourceIndexProfileName;
            dataSource.KeyFieldName = model.KeyFieldName;
            dataSource.TitleFieldName = model.TitleFieldName;
            dataSource.ContentFieldName = model.ContentFieldName;
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
