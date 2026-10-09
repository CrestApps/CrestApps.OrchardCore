using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Serves designed reports in the admin: the list of reports the user can see, running and exporting a report, and
/// deleting or duplicating one.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportDesignsController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly ReportDesignHistoryService _history;
    private readonly DesignedReportPresenter _presenter;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignsController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="history">The service that keeps drafts and versions.</param>
    /// <param name="presenter">The report presenter.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ReportDesignsController(
        ReportDesignService designService,
        ReportDesignHistoryService history,
        DesignedReportPresenter presenter,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReportDesignsController> htmlLocalizer)
    {
        _designService = designService;
        _history = history;
        _presenter = presenter;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the designed reports the user can run.
    /// </summary>
    /// <param name="q">The optional search text.</param>
    /// <returns>The list page.</returns>
    [Admin("reports/designs", "ReportDesignsIndex")]
    public async Task<IActionResult> Index(string q)
    {
        var model = new ReportDesignsIndexViewModel
        {
            CanDesign = await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns),
            Search = q,
        };
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        foreach (var design in await _designService.GetAllAsync())
        {
            if (!string.IsNullOrWhiteSpace(q) &&
                design.DisplayText?.Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase) != true &&
                design.Category?.Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase) != true)
            {
                continue;
            }

            if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
            {
                continue;
            }

            model.Entries.Add(new ReportDesignListEntry
            {
                Design = design,
                CanEdit = await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design),
                IsOwner = string.Equals(design.OwnerId, userId, StringComparison.Ordinal),
            });
        }

        if (!model.CanDesign && model.Entries.Count == 0 && string.IsNullOrWhiteSpace(q))
        {
            return Forbid();
        }

        return View(model);
    }

    /// <summary>
    /// Runs a designed report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The report page.</returns>
    [Admin("reports/designs/{id}", "ReportDesignsRun")]
    public async Task<IActionResult> Run(string id)
    {
        var design = await _designService.FindAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
        {
            return Forbid();
        }

        var canEdit = await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design);
        var model = await _presenter.BuildAsync(
            design,
            Request.Query,
            canEdit || design.AllowExport,
            format => Url.RouteUrl("ReportDesignsExport", new { id = design.ItemId, format }),
            Url.RouteUrl("ReportDesignsRun", new { id = design.ItemId }),
            HttpContext.RequestAborted);

        model.EditUrl = canEdit ? Url.RouteUrl("ReportDesignerEdit", new { id = design.ItemId }) : null;

        return View(model);
    }

    /// <summary>
    /// Exports a designed report with the filter values in the query string.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="format">The export format name.</param>
    /// <returns>The exported file.</returns>
    [Admin("reports/designs/{id}/export/{format?}", "ReportDesignsExport")]
    public async Task<IActionResult> Export(string id, string format)
    {
        var design = await _designService.FindAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design))
        {
            return Forbid();
        }

        if (!design.AllowExport && !await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design))
        {
            return Forbid();
        }

        var file = await _presenter.ExportAsync(design, Request.Query, format, HttpContext.RequestAborted);

        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Deletes a designed report with its share links, draft, and versions.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("reports/designs/{id}/delete", "ReportDesignsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var design = await _designService.FindAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design))
        {
            return Forbid();
        }

        await _history.DeleteAsync(design, User);
        await _notifier.SuccessAsync(H["The report has been deleted."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Copies a designed report into a new report owned by the user. Sharing is not copied.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>A redirect to the designer of the copy.</returns>
    [HttpPost]
    [Admin("reports/designs/{id}/duplicate", "ReportDesignsDuplicate")]
    public async Task<IActionResult> Duplicate(string id)
    {
        var design = await _designService.FindAsync(id);

        if (design is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ViewAllReportDesigns, design) ||
            !await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var copy = new ReportDesign
        {
            DisplayText = H["{0} (copy)", design.DisplayText].Value,
            Description = design.Description,
            Category = design.Category,
            Query = design.Query,
            Visuals = design.Visuals,
            AllowExport = design.AllowExport,
        };

        var result = await _designService.SaveAsync(copy, null, User, canSharePublicly: false);

        if (!result.Saved)
        {
            await _notifier.ErrorAsync(H["The report could not be copied."]);

            return RedirectToAction(nameof(Index));
        }

        await _notifier.SuccessAsync(H["The report has been copied. The copy is not shared with anybody."]);

        return RedirectToRoute("ReportDesignerEdit", new { id = result.Id });
    }
}
