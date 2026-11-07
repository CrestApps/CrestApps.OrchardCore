using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager used to resolve registered reports.</param>
    /// <param name="exportManager">The export manager used to resolve export formats.</param>
    /// <param name="filterDisplayManager">The display manager used to build and bind the report filter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor used to bind the filter from the request.</param>
    /// <param name="displayValueResolver">The resolver for typed report values.</param>
    /// <param name="localizerFactory">The factory that localizes the names of the reports.</param>
    public ReportsController(
        IReportManager reportManager,
        IReportExportManager exportManager,
        IDisplayManager<ReportFilter> filterDisplayManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        ReportDisplayValueResolver displayValueResolver,
        IStringLocalizerFactory localizerFactory)
    {
        _reportManager = reportManager;
        _exportManager = exportManager;
        _filterDisplayManager = filterDisplayManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayValueResolver = displayValueResolver;
        _localizerFactory = localizerFactory;
    }

    /// <summary>
    /// Lists the reports the current user is authorized to view.
    /// </summary>
    /// <returns>The report list view.</returns>
    [Admin("Reports", "Reports")]
    public async Task<IActionResult> Index()
    {
        var accessible = new List<ReportDescriptor>();

        foreach (var report in _reportManager.GetReports())
        {
            if (await _authorizationService.AuthorizeAsync(User, report.Permission))
            {
                accessible.Add(report);
            }
        }

        return View(new ReportsIndexViewModel
        {
            Reports = accessible
                .OrderBy(report => report.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(report => _localizerFactory.Localize(report.DisplayName).Value, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
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
        var descriptor = _reportManager.GetDescriptor(id);

        if (descriptor is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, descriptor.Permission))
        {
            return Forbid();
        }

        var filter = await BuildFilterAsync(descriptor.Name);
        var filterShape = await _filterDisplayManager.BuildEditorAsync(filter, _updateModelAccessor.ModelUpdater, false);
        var document = await _reportManager.GetReport(descriptor.Name).RunAsync(new ReportContext(filter), HttpContext.RequestAborted);
        document.Title = _localizerFactory.Localize(descriptor.DisplayName).Value;

        await _displayValueResolver.ResolveNonTableValuesAsync(document);

        return View(new ReportDisplayViewModel
        {
            Report = descriptor,
            ExportFormats = await CanExportAsync(descriptor) ? _exportManager.GetFormats() : [],
            FilterShape = filterShape,
            Document = document,
        });
    }

    /// <summary>
    /// Exports a report in the requested format (CSV by default). It needs the report's permission, and its export
    /// permission when it has one.
    /// </summary>
    /// <param name="id">The report technical name.</param>
    /// <param name="format">The export format technical name.</param>
    /// <returns>The exported file.</returns>
    [Admin("Reports/view/{id}/export/{format?}", "ReportsExport")]
    public async Task<IActionResult> Export(string id, string format)
    {
        var report = _reportManager.GetDescriptor(id);

        if (report is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, report.Permission) || !await CanExportAsync(report))
        {
            return Forbid();
        }

        var exportFormat = _exportManager.GetDescriptor(string.IsNullOrEmpty(format) ? ReportsConstants.CsvExportFormat : format);

        if (exportFormat is null)
        {
            return NotFound();
        }

        var filter = await BuildFilterAsync(report.Name);
        var document = await _reportManager.GetReport(report.Name).RunAsync(new ReportContext(filter, ReportRunMode.Export), HttpContext.RequestAborted);
        document.Title = _localizerFactory.Localize(report.DisplayName).Value;

        await _displayValueResolver.ResolveAsync(document);
        var content = _exportManager.GetFormat(exportFormat.Name).Serialize(document);
        var range = filter.GetDateRange();
        var fileName = GetFileName(report.Name, range) + exportFormat.Extension;

        return File(content, exportFormat.ContentType, fileName);
    }

    // An open-ended range names only the bound it has.
    internal static string GetFileName(string reportName, ReportDateRange range)
        => (range.FromUtc, range.ToUtc) switch
        {
            ({ } from, { } to) => string.Create(CultureInfo.InvariantCulture, $"{reportName}-{from:yyyyMMdd}-to-{to:yyyyMMdd}"),
            ({ } from, null) => string.Create(CultureInfo.InvariantCulture, $"{reportName}-from-{from:yyyyMMdd}"),
            (null, { } to) => string.Create(CultureInfo.InvariantCulture, $"{reportName}-to-{to:yyyyMMdd}"),
            _ => reportName + "-all-time",
        };

    // A report may need a permission of its own to export, beside the one that views it.
    private async Task<bool> CanExportAsync(ReportDescriptor report)
        => report.ExportPermission is null || await _authorizationService.AuthorizeAsync(User, report.ExportPermission);

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
