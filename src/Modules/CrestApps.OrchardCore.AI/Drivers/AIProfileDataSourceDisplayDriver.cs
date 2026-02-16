using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileDataSourceDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ICatalog<AIDataSource> _dataSourceStore;

    internal readonly IStringLocalizer S;

    public AIProfileDataSourceDisplayDriver(
        ICatalog<AIDataSource> dataSourceStore,
        IStringLocalizer<AIProfileDataSourceDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSources_Edit", async model =>
        {
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
