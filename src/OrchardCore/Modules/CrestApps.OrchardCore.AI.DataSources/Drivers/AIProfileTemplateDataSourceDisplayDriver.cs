using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

internal sealed class AIProfileTemplateDataSourceDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ISiteService _siteService;
    private readonly IODataValidator _oDataValidator;
    private readonly ICatalog<AIDataSource> _dataSourceStore;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDataSourceDisplayDriver(
        ISiteService siteService,
        IODataValidator oDataValidator,
        ICatalog<AIDataSource> dataSourceStore,
        IStringLocalizer<AIProfileTemplateDataSourceDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _oDataValidator = oDataValidator;
        _dataSourceStore = dataSourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        var dataSourceResult = Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSources_Edit", async model =>
        {
            await PopulateViewModelAsync(template, model);
        }).Location("Content:7%General;1")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));

        var parametersResult = Initialize<EditProfileDataSourcesViewModel>("AIProfileDataSourceParameters_Edit", async model =>
        {
            await PopulateViewModelAsync(template, model);
        }).Location("Content:10%Parameters;5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));

        return Combine(dataSourceResult, parametersResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditProfileDataSourcesViewModel();

        var metadata = template.As<DataSourceMetadata>();

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

        template.Put(metadata);

        template.Alter<AIDataSourceRagMetadata>(t =>
        {
            t.Filter = model.Filter;
            t.Strictness = model.Strictness;
            t.TopNDocuments = model.TopNDocuments;
            t.IsInScope = model.IsInScope;
        });

        return Edit(template, context);
    }

    private async Task PopulateViewModelAsync(AIProfileTemplate template, EditProfileDataSourcesViewModel model)
    {
        var ragMetadata = template.As<AIDataSourceRagMetadata>();

        var dataSourceSettings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        model.Strictness = dataSourceSettings.GetStrictness(ragMetadata.Strictness);
        model.TopNDocuments = dataSourceSettings.GetTopNDocuments(ragMetadata.TopNDocuments);
        model.IsInScope = ragMetadata.IsInScope;
        model.Filter = ragMetadata.Filter;

        var metadata = template.As<DataSourceMetadata>();
        model.DataSourceId = metadata.DataSourceId;
        model.DataSources = await _dataSourceStore.GetAllAsync();
    }
}
