using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Serves the admin Reports area: the report list, a rendered report with its filter, and report exports.
/// </summary>
[Admin]
public sealed class ReportsController : Controller
{
    private readonly IReportManager _reportManager;
    private readonly IReportExportManager _exportManager;
    private readonly IDisplayManager<ReportFilter> _filterDisplayManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ReportDisplayValueResolver _displayValueResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager used to resolve registered reports.</param>
    /// <param name="exportManager">The export manager used to resolve export formats.</param>
    /// <param name="filterDisplayManager">The display manager used to build and bind the report filter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor used to bind the filter from the request.</param>
    /// <param name="displayValueResolver">The resolver for typed report values.</param>
    public ReportsController(
        IReportManager reportManager,
        IReportExportManager exportManager,
        IDisplayManager<ReportFilter> filterDisplayManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ReportDisplayValueResolver displayValueResolver)
    {
        _reportManager = reportManager;
        _exportManager = exportManager;
        _filterDisplayManager = filterDisplayManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayValueResolver = displayValueResolver;
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
        document.Title = report.DisplayName.Value;

        await _displayValueResolver.ResolveNonTableValuesAsync(document);

        return View(new ReportDisplayViewModel
        {
            Report = report,
            ExportFormats = _exportManager.ListFormats(),
            FilterShape = filterShape,
            Document = document,
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
        document.Title = report.DisplayName.Value;

        await _displayValueResolver.ResolveAsync(document);
        var content = exportFormat.Serialize(document);
        var range = filter.GetDateRange();
        var fileName = $"{id}-{range.FromUtc:yyyyMMdd}-to-{range.ToUtc:yyyyMMdd}.{exportFormat.FileExtension}";

        return File(content, exportFormat.ContentType, fileName);
    }

    private async Task<ReportFilter> BuildFilterAsync(string reportName)
    {
        var filter = new ReportFilter
        {
            ReportName = reportName,
        };

        await _filterDisplayManager.UpdateEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);

        return filter;
    }
}
