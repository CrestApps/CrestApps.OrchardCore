using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// Runs a report with its owner's access and exports it, such as to CSV or Excel. With a media folder, the file is
/// saved in the media library (when the Media feature is on) and the output holds its path and public URL; otherwise
/// the output holds the file itself, as Base64.
/// </summary>
public sealed class ExportReportTask : ReportTaskActivity<ExportReportTask>
{
    /// <summary>
    /// The output name used when none is set.
    /// </summary>
    public const string DefaultOutputName = "ReportFile";

    private readonly ReportWorkflowService _reports;
    private readonly DesignedReportPresenter _presenter;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportReportTask"/> class.
    /// </summary>
    /// <param name="reports">The report workflow service.</param>
    /// <param name="presenter">The presenter that exports reports.</param>
    /// <param name="serviceProvider">The services, for the media store when the Media feature is on.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExportReportTask(
        ReportWorkflowService reports,
        DesignedReportPresenter presenter,
        IServiceProvider serviceProvider,
        ILogger<ExportReportTask> logger,
        IStringLocalizer<ExportReportTask> stringLocalizer)
        : base(stringLocalizer)
    {
        _reports = reports;
        _presenter = presenter;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Export Report"];

    /// <summary>
    /// Gets or sets the export format, such as <c>csv</c> or <c>xlsx</c>.
    /// </summary>
    public string Format
    {
        get => GetProperty(() => ReportsConstants.CsvExportFormat);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the media folder to save the file in, with Liquid; empty to keep the file in the output only.
    /// </summary>
    public string MediaFolder
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var design = await _reports.FindReportAsync(this, workflowContext);

        if (design is null)
        {
            _logger.LogWarning("The Export Report task of workflow '{WorkflowId}' found no report to export.", workflowContext.WorkflowId);

            return Outcome("Failed");
        }

        var file = await _presenter.ExportAsync(design, await _reports.GetFilterValuesAsync(Filters, workflowContext), Format, CancellationToken.None);

        if (file is null)
        {
            _logger.LogWarning("The Export Report task of workflow '{WorkflowId}' could not export report '{ReportId}'.", workflowContext.WorkflowId, design.ItemId);

            return Outcome("Failed");
        }

        var output = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ReportId"] = design.ItemId,
            ["Title"] = design.DisplayText,
            ["FileName"] = file.FileName,
            ["ContentType"] = file.ContentType,
            ["Format"] = file.Format,
            ["RowCount"] = file.RowCount,
            ["Size"] = file.Content.Length,
        };

        var folder = (await _reports.EvaluateAsync(MediaFolder, workflowContext))?.Trim('/', '\\', ' ');

        if (!string.IsNullOrEmpty(folder))
        {
            var mediaStore = _serviceProvider.GetService<IMediaFileStore>();

            if (mediaStore is null)
            {
                _logger.LogWarning("The Export Report task of workflow '{WorkflowId}' cannot save to the media library because the Media feature is off.", workflowContext.WorkflowId);

                return Outcome("Failed");
            }

            var path = mediaStore.Combine(folder, file.FileName);

            using (var stream = new MemoryStream(file.Content))
            {
                await mediaStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
            }

            output["Path"] = path;
            output["Url"] = mediaStore.MapPathToPublicUrl(path);
        }
        else
        {
            output["Base64"] = Convert.ToBase64String(file.Content);
        }

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? DefaultOutputName : OutputName.Trim()] = output;
        workflowContext.LastResult = output;

        return Outcome("Done");
    }
}
