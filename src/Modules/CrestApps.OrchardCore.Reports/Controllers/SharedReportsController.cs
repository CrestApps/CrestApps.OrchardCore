using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Shows designed reports outside the admin: to the people and roles a report is shared with (including anonymous
/// visitors when it is shared with the Anonymous role), and to anyone holding an active share link.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
public sealed class SharedReportsController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly ReportShareLinkService _shareLinks;
    private readonly DesignedReportPresenter _presenter;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedReportsController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="shareLinks">The share link service.</param>
    /// <param name="presenter">The report presenter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SharedReportsController(
        ReportDesignService designService,
        ReportShareLinkService shareLinks,
        DesignedReportPresenter presenter,
        IAuthorizationService authorizationService,
        IClock clock,
        ILogger<SharedReportsController> logger)
    {
        _designService = designService;
        _shareLinks = shareLinks;
        _presenter = presenter;
        _authorizationService = authorizationService;
        _clock = clock;
        _logger = logger;
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
            HttpContext.RequestAborted);

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

        return await ExportAsync(design, format);
    }

    /// <summary>
    /// Shows the report a share link opens.
    /// </summary>
    /// <param name="token">The secret token of the link.</param>
    /// <returns>The report page.</returns>
    [HttpGet("reports/shared/{token}", Name = "ReportsSharedLink")]
    public async Task<IActionResult> Link(string token)
    {
        var (link, design, failure) = await ResolveLinkAsync(token);

        if (failure is not null)
        {
            return failure;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Share link '{LinkId}' opened designed report '{ReportId}' for '{UserName}'.", link.ItemId, design.ItemId, User.Identity?.Name ?? "anonymous");
        }

        var model = await _presenter.BuildAsync(
            design,
            Request.Query,
            link.AllowExport,
            format => Url.RouteUrl("ReportsSharedLinkExport", new { token, format }),
            Url.RouteUrl("ReportsSharedLink", new { token }),
            HttpContext.RequestAborted);

        model.IsPublic = true;
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        return View("Report", model);
    }

    /// <summary>
    /// Exports the report a share link opens, when the link allows it.
    /// </summary>
    /// <param name="token">The secret token of the link.</param>
    /// <param name="format">The export format name.</param>
    /// <returns>The exported file.</returns>
    [HttpGet("reports/shared/{token}/export/{format}", Name = "ReportsSharedLinkExport")]
    public async Task<IActionResult> LinkExport(string token, string format)
    {
        var (link, design, failure) = await ResolveLinkAsync(token);

        if (failure is not null)
        {
            return failure;
        }

        if (!link.AllowExport)
        {
            return NotFound();
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Share link '{LinkId}' exported designed report '{ReportId}' as '{Format}'.", link.ItemId, design.ItemId, format?.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal));
        }

        return await ExportAsync(design, format);
    }

    private async Task<(ReportShareLink Link, ReportDesign Design, IActionResult Failure)> ResolveLinkAsync(string token)
    {
        var link = await _shareLinks.FindActiveAsync(token);

        if (link is null)
        {
            _logger.LogInformation("A request used an unknown, expired, or revoked report share link.");

            return (null, null, NotFound());
        }

        if (link.RequireSignIn && User.Identity?.IsAuthenticated != true)
        {
            return (null, null, Challenge());
        }

        var design = await _designService.FindAsync(link.ReportId);

        return design is null
            ? (null, null, NotFound())
            : (link, design, null);
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

    private async Task<IActionResult> ExportAsync(ReportDesign design, string format)
    {
        var file = await _presenter.ExportAsync(design, Request.Query, format, HttpContext.RequestAborted);

        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    private IActionResult NotFoundOrChallenge()
    {
        return User.Identity?.IsAuthenticated == true ? NotFound() : Challenge();
    }
}
