using CrestApps.OrchardCore.Reports.Designer;
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
/// Lists and deletes the reusable views of the report designer.
/// </summary>
[Feature(ReportsConstants.DesignerFeature)]
[Admin]
public sealed class ReportViewsController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewsController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ReportViewsController(
        ReportDesignService designService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReportViewsController> htmlLocalizer)
    {
        _designService = designService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
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

        return View(model);
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
