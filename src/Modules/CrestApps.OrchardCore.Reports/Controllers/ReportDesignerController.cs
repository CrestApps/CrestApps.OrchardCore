using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Security.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Serves the drag-and-drop designer for reports and views: the designer pages, saving, and the live preview.
/// </summary>
[Feature(ReportsConstants.DesignerFeature)]
[Admin]
public sealed class ReportDesignerController : Controller
{
    /// <summary>
    /// The largest designer payload accepted, in bytes.
    /// </summary>
    internal const int MaxPayloadBytes = 1024 * 1024;

    private readonly ReportDesignService _designService;
    private readonly ReportDesignRunner _runner;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRoleService _roleService;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignerController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="runner">The report runner used by the preview.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="roleService">The role service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignerController(
        ReportDesignService designService,
        ReportDesignRunner runner,
        IAuthorizationService authorizationService,
        IRoleService roleService,
        IStringLocalizer<ReportDesignerController> stringLocalizer)
    {
        _designService = designService;
        _runner = runner;
        _authorizationService = authorizationService;
        _roleService = roleService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Opens the designer for a new report.
    /// </summary>
    /// <returns>The designer page.</returns>
    [Admin("reports/designer/create", "ReportDesignerCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        return View("Designer", await BuildViewModelAsync(new ReportDesignerPayload
        {
            Visuals =
            [
                new ReportVisualDefinition
                {
                    Id = "table",
                    Type = ReportVisualType.Table,
                    ShowTotals = true,
                },
            ],
        }, isView: false));
    }

    /// <summary>
    /// Opens the designer for a stored report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The designer page.</returns>
    [Admin("reports/designer/edit/{id}", "ReportDesignerEdit")]
    public async Task<IActionResult> Edit(string id)
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

        return View("Designer", await BuildViewModelAsync(ReportDesignerPayload.From(design), isView: false));
    }

    /// <summary>
    /// Opens the designer for a new view.
    /// </summary>
    /// <returns>The designer page.</returns>
    [Admin("reports/views/create", "ReportViewsCreate")]
    public async Task<IActionResult> CreateView()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        return View("Designer", await BuildViewModelAsync(new ReportDesignerPayload(), isView: true));
    }

    /// <summary>
    /// Opens the designer for a stored view.
    /// </summary>
    /// <param name="id">The view identifier.</param>
    /// <returns>The designer page.</returns>
    [Admin("reports/views/edit/{id}", "ReportViewsEdit")]
    public async Task<IActionResult> EditView(string id)
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

        return View("Designer", await BuildViewModelAsync(ReportDesignerPayload.From(view), isView: true));
    }

    /// <summary>
    /// Saves a report sent by the designer.
    /// </summary>
    /// <returns>The outcome as JSON.</returns>
    [HttpPost]
    [Admin("reports/designer/save", "ReportDesignerSave")]
    public async Task<IActionResult> Save()
    {
        var payload = await ReadPayloadAsync();

        if (payload is null)
        {
            return BadRequest();
        }

        ReportDesign existing = null;

        if (!string.IsNullOrEmpty(payload.Id))
        {
            existing = await _designService.FindAsync(payload.Id);

            if (existing is null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, existing))
            {
                return Forbid();
            }
        }
        else if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var canSharePublicly = await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ShareReportsPublicly);
        var result = await _designService.SaveAsync(payload.ToDesign(), existing, User, canSharePublicly);

        return Json(new
        {
            result.Id,
            result.Saved,
            result.Errors,
            result.Warnings,
            EditUrl = result.Saved ? Url.RouteUrl("ReportDesignerEdit", new { id = result.Id }) : null,
            RunUrl = result.Saved ? Url.RouteUrl("ReportDesignsRun", new { id = result.Id }) : null,
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Saves a view sent by the designer.
    /// </summary>
    /// <returns>The outcome as JSON.</returns>
    [HttpPost]
    [Admin("reports/views/save", "ReportViewsSave")]
    public async Task<IActionResult> SaveView()
    {
        var payload = await ReadPayloadAsync();

        if (payload is null)
        {
            return BadRequest();
        }

        ReportView existing = null;

        if (!string.IsNullOrEmpty(payload.Id))
        {
            existing = await _designService.FindViewAsync(payload.Id);

            if (existing is null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, existing))
            {
                return Forbid();
            }
        }
        else if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var result = await _designService.SaveViewAsync(payload.ToView(), existing, User);

        return Json(new
        {
            result.Id,
            result.Saved,
            result.Errors,
            result.Warnings,
            EditUrl = result.Saved ? Url.RouteUrl("ReportViewsEdit", new { id = result.Id }) : null,
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Runs the report or view being designed with the designer's access and renders the result.
    /// </summary>
    /// <param name="view">Whether a view is being previewed.</param>
    /// <returns>The rendered preview.</returns>
    [HttpPost]
    [Admin("reports/designer/preview", "ReportDesignerPreview")]
    public async Task<IActionResult> Preview(bool view = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var payload = await ReadPayloadAsync();

        if (payload is null)
        {
            return BadRequest();
        }

        var design = payload.ToDesign();

        design.DisplayText ??= S["Preview"];
        design.Query = ReportDesignNormalizer.Normalize(design.Query);
        design.Visuals = view ? [] : ReportDesignNormalizer.Normalize(design.Visuals);

        var filterValues = payload.FilterValues?.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var run = await _runner.RunAsync(design, User, filterValues, HttpContext.RequestAborted);

        return PartialView("_DesignerPreview", new ReportDesignerPreviewViewModel
        {
            Run = run,
            IsView = view,
        });
    }

    private async Task<ReportDesignerPayload> ReadPayloadAsync()
    {
        var body = await RequestBodyReader.ReadAsync(Request, MaxPayloadBytes, HttpContext.RequestAborted);

        if (body.IsTooLarge || string.IsNullOrWhiteSpace(body.Body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportDesignerPayload>(body.Body, ReportDesignerJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ReportDesignerViewModel> BuildViewModelAsync(ReportDesignerPayload payload, bool isView)
    {
        var roles = (await _roleService.GetRoleNamesAsync()).ToList();

        foreach (var systemRole in new[] { OrchardCoreConstants.Roles.Anonymous, OrchardCoreConstants.Roles.Authenticated })
        {
            if (!roles.Contains(systemRole, StringComparer.OrdinalIgnoreCase))
            {
                roles.Add(systemRole);
            }
        }

        var categories = (await _designService.GetAllAsync())
            .Select(design => design.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ReportDesignerViewModel
        {
            IsView = isView,
            Payload = payload,
            CanSharePublicly = await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ShareReportsPublicly),
            Roles = roles.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            Categories = categories,
            Labels = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["text"] = ReportDesignerTexts.Build(S),
                ["aggregateLabels"] = ReportDesignerTexts.Aggregates(S),
                ["transformLabels"] = ReportDesignerTexts.Transforms(S),
                ["operatorLabels"] = ReportDesignerTexts.Operators(S),
                ["controlLabels"] = ReportDesignerTexts.Controls(S),
                ["chartLabels"] = ReportDesignerTexts.Charts(S),
                ["visualLabels"] = ReportDesignerTexts.Visuals(S),
            },
        };
    }
}
