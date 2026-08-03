using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

/// <summary>
/// Contributes the data source selection and retrieval configuration to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Knowledge tab.
/// </summary>
public sealed class AICompletionWithConfigDataSourceDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly ISiteService _siteService;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IODataValidator _oDataValidator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="oDataValidator">The OData validator.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigDataSourceDisplayDriver(
        ISiteService siteService,
        IAIDataSourceStore dataSourceStore,
        IODataValidator oDataValidator,
        IStringLocalizer<AICompletionWithConfigDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _dataSourceStore = dataSourceStore;
        _oDataValidator = oDataValidator;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        var selectorResult = Initialize<AICompletionWithConfigDataSourceViewModel>("AICompletionWithConfigDataSource_Edit", async model =>
        {
            await PopulateAsync(activity, model);
        }).Location("Content:1#Knowledge;2");

        var retrievalResult = Initialize<AICompletionWithConfigDataSourceViewModel>("AICompletionWithConfigDataSourceRetrieval_Edit", async model =>
        {
            await PopulateAsync(activity, model);
        }).Location("Content:2#Knowledge;2");

        return Combine(selectorResult, retrievalResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var interaction = activity.Interaction;

        if (!string.IsNullOrEmpty(model.DataSourceId))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(model.DataSourceId);

            if (dataSource != null)
            {
                interaction.Alter<DataSourceMetadata>(metadata =>
                {
                    metadata.DataSourceId = dataSource.ItemId;
                });
            }
        }
        else
        {
            interaction.Alter<DataSourceMetadata>(metadata => metadata.DataSourceId = null);
        }

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        var strictness = dataSourceSettings.GetStrictness(model.Strictness);
        var topN = dataSourceSettings.GetTopNDocuments(model.TopNDocuments);

        if (strictness != model.Strictness)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Strictness),
            S["Invalid strictness value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinStrictness, AIDataSourceSettings.MaxStrictness]);
        }

        if (topN != model.TopNDocuments)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TopNDocuments),
            S["Invalid total retrieved documents value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinTopNDocuments, AIDataSourceSettings.MaxTopNDocuments]);
        }

        if (!string.IsNullOrWhiteSpace(model.Filter) && !_oDataValidator.IsValidFilter(model.Filter))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Filter), S["Invalid filter value. It must be a valid OData filter."]);
        }

        interaction.Alter<AIDataSourceRagMetadata>(metadata =>
        {
            metadata.Strictness = strictness;
            metadata.TopNDocuments = topN;
            metadata.IsInScope = model.IsInScope;
            metadata.Filter = model.Filter;
        });

        activity.Interaction = interaction;

        return Edit(activity, context);
    }

    private async Task PopulateAsync(AICompletionWithConfigTask activity, AICompletionWithConfigDataSourceViewModel model)
    {
        var interaction = activity.Interaction;

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        var metadata = interaction.GetOrCreate<DataSourceMetadata>();
        model.DataSourceId = metadata?.DataSourceId;

        var ragMetadata = interaction.GetOrCreate<AIDataSourceRagMetadata>();

        model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
        model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
        model.IsInScope = ragMetadata.IsInScope;
        model.Filter = ragMetadata.Filter;

        model.DataSources = await _dataSourceStore.GetAllAsync();
    }
}
