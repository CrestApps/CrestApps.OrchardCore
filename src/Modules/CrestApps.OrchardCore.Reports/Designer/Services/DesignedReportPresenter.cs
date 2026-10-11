using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Http;
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
    private readonly IClock _clock;

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
        IClock clock)
    {
        _runner = runner;
        _exportManager = exportManager;
        _displayValueResolver = displayValueResolver;
        _clock = clock;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);

        var filterValues = ReportFilterValueBinder.Bind(query, design.Query?.Filters);
        var run = await _runner.RunAsync(design, filterValues, cancellationToken);

        if (run.Document is not null)
        {
            await _displayValueResolver.ResolveNonTableValuesAsync(run.Document);
        }

        return new DesignedReportViewModel
        {
            Design = design,
            Run = run,
            ExportFormats = canExport ? _exportManager.GetFormats() : [],
            ExportUrl = exportUrl,
            FormAction = formAction,
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);

        var exportFormat = _exportManager.FindFormat(string.IsNullOrEmpty(format) ? ReportsConstants.CsvExportFormat : format);

        if (exportFormat is null)
        {
            return null;
        }

        var filterValues = ReportFilterValueBinder.Bind(query, design.Query?.Filters);
        var run = await _runner.RunAsync(design, filterValues, cancellationToken);

        if (run.Document is null)
        {
            return null;
        }

        await _displayValueResolver.ResolveAsync(run.Document);

        return new ReportExportFile(
            exportFormat.Serialize(run.Document),
            exportFormat.ContentType,
            $"{FileName(design.DisplayText)}-{_clock.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.{exportFormat.FileExtension}");
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
public sealed record ReportExportFile(byte[] Content, string ContentType, string FileName);
