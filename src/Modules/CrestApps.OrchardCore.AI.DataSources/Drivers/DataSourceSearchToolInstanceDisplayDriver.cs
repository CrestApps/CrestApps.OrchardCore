using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

/// <summary>
/// Display driver that captures the settings for the built-in data source search tool instance source. The
/// instance exposes one existing AI data source to the model as a callable vector search function, carrying
/// its own retrieval parameters rather than reading them from a profile.
/// </summary>
internal sealed class DataSourceSearchToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly ISiteService _siteService;
    private readonly IODataValidator _oDataValidator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceSearchToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="oDataValidator">The OData validator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DataSourceSearchToolInstanceDisplayDriver(
        IAIDataSourceStore dataSourceStore,
        ISiteService siteService,
        IODataValidator oDataValidator,
        IStringLocalizer<DataSourceSearchToolInstanceDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        _siteService = siteService;
        _oDataValidator = oDataValidator;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        return Initialize<DataSourceSearchToolInstanceViewModel>("DataSourceSearchToolInstance_Edit", async model =>
        {
            var settings = instance.GetOrCreate<DataSourceSearchToolSettings>();

            model.DataSourceId = settings.DataSourceId;
            model.RetrievalMode = settings.RetrievalMode;
            model.TopNDocuments = settings.TopNDocuments;
            model.Strictness = settings.Strictness;
            model.Filter = settings.Filter;
            model.DataSources = await _dataSourceStore.GetAllAsync();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        var model = new DataSourceSearchToolInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DataSourceId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataSourceId), S["A data source is required."]);
        }
        else if (await _dataSourceStore.FindByIdAsync(model.DataSourceId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataSourceId), S["Invalid data source provided."]);
        }

        // Unlike a profile, an unset value here is not stored as the default in effect today: it keeps
        // reading whatever the site settings say, so only a value the user actually typed is range-checked.
        var settings = await _siteService.GetSettingsAsync<AIDataSourceSettings>();

        if (model.Strictness.HasValue && settings.GetStrictness(model.Strictness) != model.Strictness)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Strictness),
                S["Invalid strictness value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinStrictness, AIDataSourceSettings.MaxStrictness]);
        }

        if (model.TopNDocuments.HasValue && settings.GetTopNDocuments(model.TopNDocuments) != model.TopNDocuments)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TopNDocuments),
                S["Invalid total retrieved documents value. A valid value must be between {0} and {1}.", AIDataSourceSettings.MinTopNDocuments, AIDataSourceSettings.MaxTopNDocuments]);
        }

        if (!string.IsNullOrWhiteSpace(model.Filter) && !_oDataValidator.IsValidFilter(model.Filter))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Filter), S["Invalid filter value. It must be a valid OData filter."]);
        }

        instance.Put(new DataSourceSearchToolSettings
        {
            DataSourceId = model.DataSourceId,
            RetrievalMode = model.RetrievalMode,
            TopNDocuments = model.TopNDocuments,
            Strictness = model.Strictness,
            Filter = string.IsNullOrWhiteSpace(model.Filter) ? null : model.Filter.Trim(),
        });

        return Edit(instance, context);
    }

    private static bool IsSource(AIToolInstance instance)
        => string.Equals(instance.Source, DataSourceSearchToolConstants.SourceName, StringComparison.OrdinalIgnoreCase);
}
