using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Serves the admin Reports area: the report list, a rendered report with its filter, and report exports.
/// </summary>
[Admin]
public sealed class ReportsController : Controller
{
    private const int DefaultRangeDays = 30;

    private readonly IReportManager _reportManager;
    private readonly IReportExportManager _exportManager;
    private readonly IDisplayManager<ReportFilter> _filterDisplayManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager used to resolve registered reports.</param>
    /// <param name="exportManager">The export manager used to resolve export formats.</param>
    /// <param name="filterDisplayManager">The display manager used to build and bind the report filter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor used to bind the filter from the request.</param>
    /// <param name="clock">The clock used to compute the default reporting period.</param>
    public ReportsController(
        IReportManager reportManager,
        IReportExportManager exportManager,
        IDisplayManager<ReportFilter> filterDisplayManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IClock clock)
    {
        _reportManager = reportManager;
        _exportManager = exportManager;
        _filterDisplayManager = filterDisplayManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _clock = clock;
    }

    /// <summary>
    /// Lists the reports the current user is authorized to view.
    /// </summary>
    /// <returns>The report list view.</returns>
    [Admin("Reports", "Reports")]
    public async Task<IActionResult> Index()
    {
        var accessible = new List<IReport>();

        foreach (var report in _reportManager.ListReports())
        {
            if (await _authorizationService.AuthorizeAsync(User, report.Permission))
            {
                accessible.Add(report);
            }
        }

        return View(new ReportsIndexViewModel
        {
            Reports = accessible,
        });
    }

    /// <summary>
    /// Renders a report with its filter editor and the resulting document.
    /// </summary>
    /// <param name="id">The report technical name.</param>
    /// <returns>The report view.</returns>
    [Admin("Reports/view/{id}", "ReportsDisplay")]
    public async Task<IActionResult> Display(string id)
    {
        var report = _reportManager.FindByName(id);

        if (report is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, report.Permission))
        {
            return Forbid();
        }

        var filter = await BuildFilterAsync(id);
        var filterShape = await _filterDisplayManager.BuildEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);
        var document = await report.RunAsync(new ReportContext(filter), HttpContext.RequestAborted);

        return View(new ReportDisplayViewModel
        {
            Report = report,
            FilterShape = filterShape,
            Document = document,
            FromUtc = filter.FromUtc.GetValueOrDefault(),
            ToUtc = filter.ToUtc.GetValueOrDefault(),
        });
    }

    /// <summary>
    /// Exports a report in the requested format (CSV by default).
    /// </summary>
    /// <param name="id">The report technical name.</param>
    /// <param name="format">The export format technical name.</param>
    /// <returns>The exported file.</returns>
    [Admin("Reports/view/{id}/export/{format?}", "ReportsExport")]
    public async Task<IActionResult> Export(string id, string format)
    {
        var report = _reportManager.FindByName(id);

        if (report is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, report.Permission))
        {
            return Forbid();
        }

        var exportFormat = _exportManager.FindFormat(string.IsNullOrEmpty(format) ? ReportsConstants.CsvExportFormat : format);

        if (exportFormat is null)
        {
            return NotFound();
        }

        var filter = await BuildFilterAsync(id);
        var document = await report.RunAsync(new ReportContext(filter), HttpContext.RequestAborted);
        var content = exportFormat.Serialize(document);
        var fileName = $"{id}-{filter.FromUtc:yyyyMMdd}-to-{filter.ToUtc:yyyyMMdd}.{exportFormat.FileExtension}";

        return File(content, exportFormat.ContentType, fileName);
    }

    private async Task<ReportFilter> BuildFilterAsync(string reportName)
    {
        var filter = new ReportFilter
        {
            ReportName = reportName,
        };

        await _filterDisplayManager.UpdateEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);

        NormalizeRange(filter);

        return filter;
    }

    private void NormalizeRange(ReportFilter filter)
    {
        var today = _clock.UtcNow.Date;
        var to = filter.ToUtc?.Date ?? today;
        var from = filter.FromUtc?.Date ?? to.AddDays(-(DefaultRangeDays - 1));

        if (from > to)
        {
            (from, to) = (to, from);
        }

        filter.FromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        filter.ToUtc = DateTime.SpecifyKind(to.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
    }
}
