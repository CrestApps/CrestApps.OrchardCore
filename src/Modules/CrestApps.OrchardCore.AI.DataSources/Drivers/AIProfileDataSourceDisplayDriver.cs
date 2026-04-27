using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIProfileDataSourceDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly IODataValidator _oDataValidator;
    private readonly IAIDataSourceStore _dataSourceStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDataSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="oDataValidator">The o data validator.</param>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileDataSourceDisplayDriver(
        ISiteService siteService,
        IODataValidator oDataValidator,
        IAIDataSourceStore dataSourceStore,
        IStringLocalizer<AIProfileDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _oDataValidator = oDataValidator;
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        var dataSourceResult = Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSources_Edit", async model =>
        {
            await PopulateViewModelAsync(profile, model);
        }).Location("Content:4#Knowledge;2");

        var parametersResult = Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSourceParameters_Edit", async model =>
        {
            await PopulateViewModelAsync(profile, model);
        }).Location("Content:5#Knowledge;2");

        var retrievalParametersResult = Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSourceRetrieval_Edit", async model =>
        {
            await PopulateViewModelAsync(profile, model);
        }).Location("Content:6#Knowledge;2");

        return Combine(dataSourceResult, parametersResult, retrievalParametersResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileDataSourcesViewModel();

        var metadata = profile.GetOrCreate<DataSourceMetadata>();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.DataSourceId))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(model.DataSourceId);

            if (dataSource is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataSourceId), S["Invalid data source provided."]);
            }

            metadata.DataSourceId = model.DataSourceId;
        }
        else
        {
            metadata.DataSourceId = null;
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

        profile.Put(metadata);

        profile.Alter<AIDataSourceRagMetadata>(t =>
        {
            t.Filter = model.Filter;
            t.Strictness = model.Strictness;
            t.TopNDocuments = model.TopNDocuments;
            t.IsInScope = model.IsInScope;
        });

        return Edit(profile, context);
    }

    private async Task PopulateViewModelAsync(AIProfile profile, EditProfileDataSourcesViewModel model)
    {
        var ragMetadata = profile.GetOrCreate<AIDataSourceRagMetadata>();

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
        model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
        model.IsInScope = ragMetadata.IsInScope;
        model.Filter = ragMetadata.Filter;

        var metadata = profile.GetOrCreate<DataSourceMetadata>();
        model.DataSourceId = metadata.DataSourceId;
        model.DataSources = await _dataSourceStore.GetAllAsync();
    }
}
