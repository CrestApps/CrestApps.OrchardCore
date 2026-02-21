using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIProfileDataSourceDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly ICatalog<AIDataSource> _dataSourceStore;

    internal readonly IStringLocalizer S;

    public AIProfileDataSourceDisplayDriver(
        ISiteService siteService,
        ICatalog<AIDataSource> dataSourceStore,
        IStringLocalizer<AIProfileDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSources_Edit", async model =>
        {
            var ragMetadata = profile.As<AIDataSourceRagMetadata>();

            var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

            model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
            model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
            model.IsInScope = ragMetadata.IsInScope;
            model.Filter = ragMetadata.Filter;

            var metadata = profile.As<AIProfileDataSourceMetadata>();
            model.DataSourceId = metadata.DataSourceId;
            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileDataSourcesViewModel();

        var metadata = profile.As<AIProfileDataSourceMetadata>();

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

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
