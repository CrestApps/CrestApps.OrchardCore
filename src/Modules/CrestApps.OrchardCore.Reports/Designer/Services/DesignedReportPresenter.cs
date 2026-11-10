using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Runs a designed report for a page or an export, the same way whether it is opened from the admin, by a person it is
/// shared with, or through a share link.
/// </summary>
public sealed class DesignedReportPresenter
{
    private readonly ReportDesignRunner _runner;
    private readonly IReportExportManager _exportManager;
    private readonly ReportDisplayValueResolver _displayValueResolver;
    private readonly IEnumerable<IReportEventHandler> _eventHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignedReportPresenter"/> class.
    /// </summary>
    /// <param name="runner">The report runner.</param>
    /// <param name="exportManager">The export manager.</param>
    /// <param name="displayValueResolver">The resolver of typed report values.</param>
    /// <param name="clock">The clock.</param>
    public DesignedReportPresenter(
        ReportDesignRunner runner,
        IReportExportManager exportManager,
        ReportDisplayValueResolver displayValueResolver,
        IEnumerable<IReportEventHandler> eventHandlers,
        IClock clock,
        ILogger<DesignedReportPresenter> logger)
    {
        _runner = runner;
        _exportManager = exportManager;
        _displayValueResolver = displayValueResolver;
        _eventHandlers = eventHandlers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs a report for display.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="query">The request query string holding the filter values.</param>
    /// <param name="canExport">Whether the viewer may export.</param>
    /// <param name="exportUrl">Builds the export URL of a format.</param>
    /// <param name="formAction">The URL the filter form submits to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page view model.</returns>
    public async Task<DesignedReportViewModel> BuildAsync(
        ReportDesign design,
        IQueryCollection query,
        bool canExport,
        Func<string, string> exportUrl,
        string formAction,
        CancellationToken cancellationToken,
        ReportEventSource? source = null,
        string userName = null)
    {
        ArgumentNullException.ThrowIfNull(design);

        var filterValues = ReportFilterValueBinder.Bind(query, design.GetFilters());
        var options = ReportFilterValueBinder.BindSelections(query);

        options.Source = source?.ToString();

        using var activity = ReportTelemetry.Start("report.present");

        activity?.SetTag(ReportTelemetry.ReportIdTag, design.ItemId);
        activity?.SetTag(ReportTelemetry.RunSourceTag, ReportRunSources.Normalize(options.Source));

        var run = await _runner.RunAsync(design, filterValues, period: null, options, cancellationToken);

        if (run.Document is not null)
        {
            await _displayValueResolver.ResolveNonTableValuesAsync(run.Document);

            // Refreshing the visuals a click filters is the same run of the report, not another one.
            if (source.HasValue && options.VisualIds is null)
            {
                await RaiseAsync((handler, context) => handler.RanAsync(context), "ran", new ReportEventContext
                {
                    Design = design,
                    UserName = userName,
                    Source = source.Value,
                    RowCount = run.RowCount,
                    FilterValues = filterValues,
                });
            }
        }

        return new DesignedReportViewModel
        {
            Design = design,
            Run = run,
            ExportFormats = canExport ? _exportManager.GetFormats() : [],
            ExportUrl = exportUrl,
            FormAction = formAction,
            Selections = options.Selections,
        };
    }

    /// <summary>
    /// Runs a report and serializes it in an export format.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="query">The request query string holding the filter values.</param>
    /// <param name="format">The export format name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The file, or <see langword="null"/> when the format is unknown or the report cannot run.</returns>
    public async Task<ReportExportFile> ExportAsync(
        ReportDesign design,
        IQueryCollection query,
        string format,
        CancellationToken cancellationToken,
        ReportEventSource? source = null,
        string userName = null)
    {
        ArgumentNullException.ThrowIfNull(design);

        var filterValues = ReportFilterValueBinder.Bind(query, design.GetFilters());
        var options = ReportFilterValueBinder.BindSelections(query);

        // An export holds every visual, filtered by the values picked on the page it was exported from.
        options.VisualIds = null;
        options.Source = source?.ToString();

        var file = await ExportAsync(design, filterValues, format, options, cancellationToken);

        if (file is not null && source.HasValue)
        {
            await RaiseAsync((handler, context) => handler.ExportedAsync(context), "exported", new ReportEventContext
            {
                Design = design,
                UserName = userName,
                Source = source.Value,
                Format = file.Format,
                RowCount = file.RowCount,
                FilterValues = filterValues,
            });
        }

        return file;
    }

    // Tells the event handlers, such as the workflow events, about a run or an export, in a report.events span that
    // shows how long the fan-out takes.
    private async Task RaiseAsync(Func<IReportEventHandler, ReportEventContext, Task> raise, string eventName, ReportEventContext context)
    {
        using var activity = ReportTelemetry.Start("report.events");

        activity?.SetTag(ReportTelemetry.ReportIdTag, context.Design?.ItemId);
        activity?.SetTag("crestapps.report.event", eventName);

        await _eventHandlers.InvokeAsync(raise, context, _logger);
    }

    /// <summary>
    /// Runs a report with the given filter values and exports it, without telling the event handlers: for services
    /// and workflows that run reports on their own.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="filterValues">The values of its filters, by filter id.</param>
    /// <param name="format">The export format, such as <c>csv</c>; CSV when empty.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file, or <see langword="null"/> when the format is unknown or the report cannot run.</returns>
    public Task<ReportExportFile> ExportAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        string format,
        CancellationToken cancellationToken)
    {
        return ExportAsync(design, filterValues, format, options: null, cancellationToken);
    }

    private async Task<ReportExportFile> ExportAsync(
        ReportDesign design,
        IDictionary<string, IList<string>> filterValues,
        string format,
        ReportRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);

        var source = options?.Source ?? ReportRunSources.Workflow;

        if (_exportManager.GetDescriptor(string.IsNullOrEmpty(format) ? ReportsConstants.CsvExportFormat : format) is null)
        {
            ReportTelemetry.RecordExport(source, null, ReportTelemetry.OutcomeNotFound);

            return null;
        }

        var run = await _runner.RunAsync(design, filterValues, period: null, options ?? new ReportRunOptions { Source = source }, cancellationToken);

        if (run.Document is null)
        {
            return null;
        }

        await _displayValueResolver.ResolveAsync(run.Document);

        return Export(design, run, format, source);
    }

    /// <summary>
    /// Exports a report that already ran, whose display values are resolved.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="run">The result of its run.</param>
    /// <param name="format">The export format, such as <c>csv</c>; CSV when empty.</param>
    /// <param name="source">What the export is for, one of the values of <see cref="ReportRunSources"/>; a workflow when
    /// <see langword="null"/>.</param>
    /// <returns>The file, or <see langword="null"/> when the format is unknown or the run has no document.</returns>
    public ReportExportFile Export(ReportDesign design, ReportRunResult run, string format, string source = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(run);

        var descriptor = _exportManager.GetDescriptor(string.IsNullOrEmpty(format) ? ReportsConstants.CsvExportFormat : format);

        if (descriptor is null || run.Document is null)
        {
            if (descriptor is null)
            {
                ReportTelemetry.RecordExport(source ?? ReportRunSources.Workflow, null, ReportTelemetry.OutcomeNotFound);
            }

            return null;
        }

        return new ReportExportFile(
            ReportTelemetry.Serialize(_exportManager.GetFormat(descriptor.Name), descriptor, run.Document, source ?? ReportRunSources.Workflow),
            descriptor.ContentType,
            $"{FileName(design.DisplayText)}-{_clock.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}{descriptor.Extension}")
        {
            Format = descriptor.Name,
            RowCount = run.RowCount,
        };
    }

    private static string FileName(string title)
    {
        var builder = new StringBuilder();

        foreach (var character in title ?? string.Empty)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            if (builder.Length >= 60)
            {
                break;
            }
        }

        var name = builder.ToString().Trim('-');

        return name.Length == 0 ? "report" : name;
    }
}

/// <summary>
/// An exported report file.
/// </summary>
/// <param name="Content">The file content.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="FileName">The download file name.</param>
public sealed record ReportExportFile(byte[] Content, string ContentType, string FileName)
{
    /// <summary>
    /// Gets the name of the export format, such as <c>csv</c>.
    /// </summary>
    public string Format { get; init; }

    /// <summary>
    /// Gets the number of rows the report returned.
    /// </summary>
    public int RowCount { get; init; }
}
