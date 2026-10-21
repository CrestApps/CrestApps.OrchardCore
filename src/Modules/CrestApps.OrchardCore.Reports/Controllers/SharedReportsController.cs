using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Shows designed reports outside the admin to the people and roles a report is shared with, including anonymous
/// visitors when it is shared with the Anonymous role. Shared links open reports through
/// <see cref="ReportSharedLinkHandler"/>.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
public sealed class SharedReportsController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly DesignedReportPresenter _presenter;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedReportsController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="presenter">The report presenter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public SharedReportsController(
        ReportDesignService designService,
        DesignedReportPresenter presenter,
        IAuthorizationService authorizationService)
    {
        _designService = designService;
        _presenter = presenter;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Shows a report shared with the visitor.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The report page.</returns>
    [HttpGet("reports/view/{id}", Name = "ReportsSharedView")]
    public async Task<IActionResult> Shared(string id)
    {
        var design = await FindViewableAsync(id);

        if (design is null)
        {
            return NotFoundOrChallenge();
        }

        var model = await _presenter.BuildAsync(
            design,
            Request.Query,
            design.AllowExport,
            format => Url.RouteUrl("ReportsSharedViewExport", new { id = design.ItemId, format }),
            Url.RouteUrl("ReportsSharedView", new { id = design.ItemId }),
            HttpContext.RequestAborted,
            ReportEventSource.Shared,
            User.Identity?.Name);

        model.IsPublic = true;

        return View("Report", model);
    }

    /// <summary>
    /// Exports a report shared with the visitor.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="format">The export format name.</param>
    /// <returns>The exported file.</returns>
    [HttpGet("reports/view/{id}/export/{format}", Name = "ReportsSharedViewExport")]
    public async Task<IActionResult> SharedExport(string id, string format)
    {
        var design = await FindViewableAsync(id);

        if (design is null || !design.AllowExport)
        {
            return NotFoundOrChallenge();
        }

        var file = await _presenter.ExportAsync(design, Request.Query, format, HttpContext.RequestAborted, ReportEventSource.Shared, User.Identity?.Name);

        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    private async Task<ReportDesign> FindViewableAsync(string id)
    {
        var design = await _designService.FindAsync(id);

        if (design is null || !await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
        {
            return null;
        }

        return design;
    }

    private IActionResult NotFoundOrChallenge()
    {
        return User.Identity?.IsAuthenticated == true ? NotFound() : Challenge();
    }
}
