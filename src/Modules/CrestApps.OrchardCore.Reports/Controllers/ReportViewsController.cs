using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Lists, deletes, and refreshes the reusable views of the report builder.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportViewsController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly ReportViewSnapshotStore _snapshots;
    private readonly ReportViewSnapshotRefresher _refresher;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewsController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="snapshots">The store of scheduled views' results.</param>
    /// <param name="refresher">The service that refreshes a scheduled view's stored result.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportViewsController(
        ReportDesignService designService,
        ReportViewSnapshotStore snapshots,
        ReportViewSnapshotRefresher refresher,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReportViewsController> htmlLocalizer,
        IStringLocalizer<ReportViewsController> stringLocalizer)
    {
        _designService = designService;
        _snapshots = snapshots;
        _refresher = refresher;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the views.
    /// </summary>
    /// <returns>The list page.</returns>
    [Admin("reports/views", "ReportViewsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var model = new ReportViewsIndexViewModel();

        foreach (var view in await _designService.GetAllViewsAsync())
        {
            model.Entries.Add((view, await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, view)));
        }

        model.Snapshots = await _snapshots.ListStatusesAsync();

        return View(model);
    }

    /// <summary>
    /// Refreshes the stored result of a scheduled view now.
    /// </summary>
    /// <param name="id">The view identifier.</param>
    /// <returns>The refresh status as JSON: when the rows were refreshed, how many there are, and the error, if any.</returns>
    [HttpPost]
    [Admin("reports/views/{id}/refresh", "ReportViewsRefresh")]
    public async Task<IActionResult> Refresh(string id)
    {
        var view = await _designService.FindViewAsync(id);

        if (view is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, view))
        {
            return Forbid();
        }

        var result = await _refresher.RefreshAsync(view, onlyWhenDue: false, HttpContext.RequestAborted);
        var error = result.Status switch
        {
            ReportViewRefreshStatus.NotScheduled => S["The view runs live. Choose a refresh schedule and save the view first."].Value,
            ReportViewRefreshStatus.Busy => S["The view is being refreshed already. Try again in a moment."].Value,
            ReportViewRefreshStatus.Failed => result.Snapshot?.LastError,
            _ => null,
        };

        return Json(new
        {
            result.Status,
            RefreshedUtc = result.Snapshot?.RefreshedUtc,
            RowCount = result.Snapshot?.RowCount ?? 0,
            Error = error,
            LastErrorUtc = result.Status == ReportViewRefreshStatus.Failed ? result.Snapshot?.LastErrorUtc : null,
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Deletes a view that no report or view reads.
    /// </summary>
    /// <param name="id">The view identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("reports/views/{id}/delete", "ReportViewsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var view = await _designService.FindViewAsync(id);

        if (view is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, view))
        {
            return Forbid();
        }

        var usages = await _designService.FindViewUsagesAsync(view.ItemId);

        if (usages.Count > 0)
        {
            await _notifier.ErrorAsync(H["The view cannot be deleted because these reports or views read it: {0}.", string.Join(", ", usages)]);

            return RedirectToAction(nameof(Index));
        }

        await _designService.DeleteViewAsync(view);
        await _notifier.SuccessAsync(H["The view has been deleted."]);

        return RedirectToAction(nameof(Index));
    }
}
