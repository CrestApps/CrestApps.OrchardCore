using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Routing;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The attendance exceptions of the teams the user manages.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class AttendanceController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAttendanceService _attendance;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttendanceController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="attendance">The attendance service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AttendanceController(
        IAuthorizationService authorizationService,
        IAttendanceService attendance,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        INotifier notifier,
        IHtmlLocalizer<AttendanceController> htmlLocalizer,
        IStringLocalizer<AttendanceController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _attendance = attendance;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists attendance exceptions.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/attendance", "WorkforceAttendance")]
    public async Task<IActionResult> Index(AttendanceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();

        // Open exceptions by default; "all" shows every status.
        AttendanceExceptionStatus? status = model.Status == "all" ? null : Enum.TryParse<AttendanceExceptionStatus>(model.Status, out var parsed) ? parsed : AttendanceExceptionStatus.Open;
        AttendanceExceptionKind? kind = Enum.TryParse<AttendanceExceptionKind>(model.Kind, out var parsedKind) ? parsedKind : null;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _attendance.PageAsync(new AttendanceQuery { TeamIds = teamIds, WorkerId = model.WorkerId, Status = status, Kind = kind }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var rows = new List<AttendanceRowViewModel>();

        foreach (var exception in items)
        {
            rows.Add(new AttendanceRowViewModel
            {
                Exception = exception,
                Worker = await _workerManager.FindByIdAsync(exception.WorkerId),
                TeamName = teams.GetValueOrDefault(exception.TeamId ?? string.Empty),
            });
        }

        model.List = new AdminListContext<AttendanceRowViewModel>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId }, { "status", model.Status }, { "kind", model.Kind }, { "workerId", model.WorkerId } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["Open"], string.Empty, status == AttendanceExceptionStatus.Open),
            new SelectListItem(S["Confirmed"], nameof(AttendanceExceptionStatus.Confirmed), status == AttendanceExceptionStatus.Confirmed),
            new SelectListItem(S["Excused"], nameof(AttendanceExceptionStatus.Excused), status == AttendanceExceptionStatus.Excused),
            new SelectListItem(S["All"], "all", status is null),
        ];
        model.KindOptions = [new SelectListItem(S["Every kind"], string.Empty, kind is null), .. AttendanceKinds.Options(S, kind)];
        model.BulkActions =
        [
            new SelectListItem(S["Confirm"], "Confirm"),
            new SelectListItem(S["Excuse"], "Excuse"),
        ];

        return View(model);
    }

    /// <summary>
    /// Confirms or excuses the exceptions ticked in the list.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="bulkAction">The action.</param>
    /// <param name="itemIds">The exceptions.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("workforce/attendance", "WorkforceAttendance")]
    public async Task<IActionResult> IndexPost(AttendanceListViewModel model, [FromForm(Name = "Options.BulkAction")] string bulkAction, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var allowed = (await _attendance.PageAsync(new AttendanceQuery { TeamIds = scope.All ? null : scope.TeamIds.ToArray() }, 0, 1000, HttpContext.RequestAborted)).Items
            .Select(exception => exception.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        var done = 0;

        foreach (var id in (itemIds ?? []).Where(allowed.Contains))
        {
            var result = bulkAction == "Excuse"
                ? await _attendance.ExcuseAsync(id, null, User.Identity?.Name, HttpContext.RequestAborted)
                : await _attendance.ConfirmAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                done++;
            }
        }

        if (done > 0)
        {
            await _notifier.SuccessAsync(bulkAction == "Excuse"
                ? H.Plural(done, "1 exception has been excused.", "{0} exceptions have been excused.", done)
                : H.Plural(done, "1 exception has been confirmed.", "{0} exceptions have been confirmed.", done));
        }

        return RedirectToAction(nameof(Index), new { teamId = model.TeamId, status = model.Status, kind = model.Kind, workerId = model.WorkerId });
    }

    /// <summary>
    /// Confirms or excuses one exception.
    /// </summary>
    /// <param name="id">The exception.</param>
    /// <param name="decision">Confirm or Excuse.</param>
    /// <param name="note">Why, for an excuse.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>A redirect back.</returns>
    [HttpPost]
    [Admin("workforce/attendance/{id}/decide", "WorkforceAttendanceDecide")]
    public async Task<IActionResult> Decide(string id, string decision, string note, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var exception = (await _attendance.PageAsync(new AttendanceQuery { TeamIds = scope.All ? null : scope.TeamIds.ToArray() }, 0, 1000, HttpContext.RequestAborted)).Items
            .FirstOrDefault(item => item.ItemId == id);

        if (exception is null)
        {
            return NotFound();
        }

        var result = decision == "Excuse"
            ? await _attendance.ExcuseAsync(id, note, User.Identity?.Name, HttpContext.RequestAborted)
            : await _attendance.ConfirmAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(decision == "Excuse" ? H["The exception has been excused."] : H["The exception has been confirmed."]);
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// The names of attendance exception kinds.
/// </summary>
internal static class AttendanceKinds
{
    public static string Name(IStringLocalizer S, AttendanceExceptionKind kind)
    {
        return kind switch
        {
            AttendanceExceptionKind.Late => S["Late start"],
            AttendanceExceptionKind.EarlyOut => S["Early finish"],
            AttendanceExceptionKind.MissedPunch => S["Missed punch"],
            AttendanceExceptionKind.NoShow => S["No-show"],
            AttendanceExceptionKind.UnscheduledAbsence => S["Unscheduled absence"],
            AttendanceExceptionKind.LongMeal => S["Long meal"],
            _ => S["Away from the site"],
        };
    }

    public static string Name(IHtmlLocalizer H, AttendanceExceptionKind kind)
    {
        return kind switch
        {
            AttendanceExceptionKind.Late => H["Late start"].Value,
            AttendanceExceptionKind.EarlyOut => H["Early finish"].Value,
            AttendanceExceptionKind.MissedPunch => H["Missed punch"].Value,
            AttendanceExceptionKind.NoShow => H["No-show"].Value,
            AttendanceExceptionKind.UnscheduledAbsence => H["Unscheduled absence"].Value,
            AttendanceExceptionKind.LongMeal => H["Long meal"].Value,
            _ => H["Away from the site"].Value,
        };
    }

    public static IEnumerable<SelectListItem> Options(IStringLocalizer S, AttendanceExceptionKind? selected)
    {
        return Enum.GetValues<AttendanceExceptionKind>().Select(kind => new SelectListItem(Name(S, kind), kind.ToString(), kind == selected));
    }
}
