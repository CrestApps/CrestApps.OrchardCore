using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Services;
using CrestApps.OrchardCore.AI.DataSources.FileSources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Drivers;

/// <summary>
/// Display driver for the fields every file source connector shares: name, target File data source,
/// enabled flag, schedule, and what ingestion is allowed to spend on each file it reads.
/// </summary>
/// <remarks>
/// Renders only for records whose source names a registered ingestion connector. The same
/// <c>WebCrawler</c> record type also stores web crawlers, and a crawler's editor is not this one.
/// </remarks>
internal sealed class FileSourceDisplayDriver : DisplayDriver<WebCrawler>
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IReadOnlyList<IngestionConnectorDescriptor> _connectors;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataSourceStore">The AI data source store.</param>
    /// <param name="deploymentStore">The AI deployment store.</param>
    /// <param name="connectorOptions">The registered ingestion connectors.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileSourceDisplayDriver(
        IAIDataSourceStore dataSourceStore,
        IAIDeploymentStore deploymentStore,
        IOptions<IngestionConnectorOptions> connectorOptions,
        IStringLocalizer<FileSourceDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        _deploymentStore = deploymentStore;
        _connectors = connectorOptions.Value.Connectors;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(WebCrawler fileSource, BuildDisplayContext context)
    {
        if (!IsFileSource(fileSource))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return CombineAsync(
            View("FileSource_Fields_SummaryAdmin", fileSource).Location("Content:1"),
            View("FileSource_Buttons_SummaryAdmin", fileSource).Location("Actions:5"),
            View("FileSource_DefaultMeta_SummaryAdmin", fileSource).Location("Meta:5"),
            View("FileSource_ActionsMenu_SummaryAdmin", fileSource).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(WebCrawler fileSource, BuildEditorContext context)
    {
        if (!IsFileSource(fileSource))
        {
            return null;
        }

        return Initialize<FileSourceFieldsViewModel>("FileSourceFields_Edit", async model =>
        {
            model.DisplayText = fileSource.DisplayText;
            model.AIDataSourceId = fileSource.AIDataSourceId;
            model.Enabled = fileSource.Enabled;
            model.ReindexIntervalMinutes = fileSource.ReindexIntervalMinutes;

            var metadata = fileSource.GetOrCreate<IndexerMetadata>();
            model.FigureMode = metadata.FigureMode;
            model.VisionDeploymentName = metadata.VisionDeploymentName;
            model.UtilityDeploymentName = metadata.UtilityDeploymentName;
            model.MaxFigureDescriptionsPerDocument = metadata.MaxFigureDescriptionsPerDocument;
            model.MaxItemsPerRun = metadata.MaxItemsPerRun;
            model.Language = metadata.Language;

            var dataSources = await _dataSourceStore.GetAsync(AIDataSourceSourceTypes.File);

            model.DataSources = dataSources
                .OrderBy(dataSource => dataSource.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(dataSource => new SelectListItem(dataSource.DisplayText, dataSource.ItemId))
                .ToArray();

            model.HasDataSources = model.DataSources.Any();

            model.Deployments = (await _deploymentStore.GetAllAsync())
                .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
                .Select(deployment => new SelectListItem(deployment.Name, deployment.Name))
                .ToArray();

            model.FigureModes =
            [
                new SelectListItem(S["Auto - describe a figure when it looks worth describing"], nameof(FigureProcessingMode.Auto)),
                new SelectListItem(S["All - describe every figure that is kept"], nameof(FigureProcessingMode.All)),
                new SelectListItem(S["Off - ignore figures entirely"], nameof(FigureProcessingMode.Off)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler fileSource, UpdateEditorContext context)
    {
        if (!IsFileSource(fileSource))
        {
            return null;
        }

        var model = new FileSourceFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.AIDataSourceId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIDataSourceId), S["A target File data source is required."]);
        }

        if (model.ReindexIntervalMinutes is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReindexIntervalMinutes), S["The re-read interval must be a positive number of minutes."]);
        }

        if (model.MaxFigureDescriptionsPerDocument < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxFigureDescriptionsPerDocument), S["The figure limit cannot be negative."]);
        }

        if (model.MaxItemsPerRun is < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxItemsPerRun), S["The number of items per run must be a positive number."]);
        }

        fileSource.DisplayText = model.DisplayText?.Trim();
        fileSource.AIDataSourceId = model.AIDataSourceId?.Trim();
        fileSource.Enabled = model.Enabled;
        fileSource.ReindexIntervalMinutes = model.ReindexIntervalMinutes;

        fileSource.Put(new IndexerMetadata
        {
            FigureMode = model.FigureMode,
            VisionDeploymentName = Trimmed(model.VisionDeploymentName),
            UtilityDeploymentName = Trimmed(model.UtilityDeploymentName),
            MaxFigureDescriptionsPerDocument = Math.Max(0, model.MaxFigureDescriptionsPerDocument),
            MaxItemsPerRun = model.MaxItemsPerRun,
            Language = Trimmed(model.Language),
        });

        return Edit(fileSource, context);
    }

    private static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool IsFileSource(WebCrawler fileSource)
        => FileSourceRecords.IsConnector(fileSource.Source, _connectors);
}
