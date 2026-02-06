using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileDataSourceDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly AIOptions _options;

    internal readonly IStringLocalizer S;

    public AIProfileDataSourceDisplayDriver(
        IOptions<AIOptions> options,
        IAIDataSourceStore dataSourceStore,
        IStringLocalizer<AIProfileDataSourceDisplayDriver> stringLocalizer)
    {
        _options = options.Value;
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        var entries = _options.DataSources.Values.Where(x => string.Equals(x.ProfileSource, profile.Source, StringComparison.Ordinal));

        if (!entries.Any())
        {
            return null;
        }

        return Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSources_Edit", async model =>
        {
            var metadata = profile.As<AIProfileDataSourceMetadata>();
            model.DataSourceId = metadata.DataSourceId;
            model.DataSources = await _dataSourceStore.GetAsync(profile.Source);
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var entries = _options.DataSources.Values.Where(x => string.Equals(x.ProfileSource, profile.Source, StringComparison.Ordinal));

        if (!entries.Any())
        {
            return null;
        }

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
            metadata.DataSourceType = dataSource?.Type;
        }
        else
        {
            metadata.DataSourceId = null;
            metadata.DataSourceType = null;
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
