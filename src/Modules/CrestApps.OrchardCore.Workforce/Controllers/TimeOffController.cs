using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
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
/// The time off of the teams the user manages: the queue, requesting on a worker's behalf, deciding, and a worker's
/// balances and ledger.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class TimeOffController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeOffRequestService _requestService;
    private readonly ITimeOffBalanceService _balanceService;
    private readonly ITimeOffTypeManager _typeManager;
    private readonly ITimeOffPolicyManager _policyManager;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IShiftService _shiftService;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="requestService">The time-off request service.</param>
    /// <param name="balanceService">The time-off balance service.</param>
    /// <param name="typeManager">The time-off type manager.</param>
    /// <param name="policyManager">The time-off policy manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="shiftService">The published shift service.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffController(
        IAuthorizationService authorizationService,
        ITimeOffRequestService requestService,
        ITimeOffBalanceService balanceService,
        ITimeOffTypeManager typeManager,
        ITimeOffPolicyManager policyManager,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        IShiftService shiftService,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<TimeOffController> htmlLocalizer,
        IStringLocalizer<TimeOffController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _requestService = requestService;
        _balanceService = balanceService;
        _typeManager = typeManager;
        _policyManager = policyManager;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _shiftService = shiftService;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the time-off requests of the teams the user manages: those waiting by default, the earliest first.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-off", "WorkforceTimeOff")]
    public async Task<IActionResult> Index(TimeOffQueueViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        TimeOffRequestStatus? status = Enum.TryParse<TimeOffRequestStatus>(model.Status, out var parsed) ? parsed : null;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, requests) = await _requestService.PageAsync(new TimeOffRequestQuery { TeamIds = teamIds, Status = status, WorkerId = model.WorkerId }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<TimeOffRequestRowViewModel>
        {
            Source = await RowsAsync(requests),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId }, { "status", model.Status }, { "workerId", model.WorkerId } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["Waiting"], string.Empty, status is null),
            new SelectListItem(S["Pending"], nameof(TimeOffRequestStatus.Pending), status == TimeOffRequestStatus.Pending),
            new SelectListItem(S["Waitlisted"], nameof(TimeOffRequestStatus.Waitlisted), status == TimeOffRequestStatus.Waitlisted),
            new SelectListItem(S["Approved"], nameof(TimeOffRequestStatus.Approved), status == TimeOffRequestStatus.Approved),
            new SelectListItem(S["Declined"], nameof(TimeOffRequestStatus.Declined), status == TimeOffRequestStatus.Declined),
            new SelectListItem(S["Canceled"], nameof(TimeOffRequestStatus.Canceled), status == TimeOffRequestStatus.Canceled),
            new SelectListItem(S["Withdrawn"], nameof(TimeOffRequestStatus.Withdrawn), status == TimeOffRequestStatus.Withdrawn),
        ];
        model.BulkActions =
        [
            new SelectListItem(S["Approve"], "Approve"),
            new SelectListItem(S["Decline"], "Decline"),
        ];

        return View(model);
    }

    /// <summary>
    /// Approves or declines the requests ticked in the queue.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="bulkAction">The bulk action: Approve or Decline.</param>
    /// <param name="itemIds">The requests.</param>
    /// <returns>A redirect to the queue.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("workforce/time-off", "WorkforceTimeOff")]
    public async Task<IActionResult> IndexPost(TimeOffQueueViewModel model, [FromForm(Name = "Options.BulkAction")] string bulkAction, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var done = 0;
        var failed = new List<string>();

        foreach (var id in itemIds ?? [])
        {
            var request = await _requestService.FindAsync(id, HttpContext.RequestAborted);

            if (request is null || !scope.Covers(request.TeamId) || !request.IsOpen)
            {
                continue;
            }

            var result = bulkAction == "Decline"
                ? await _requestService.DeclineAsync(id, User.Identity?.Name, null, HttpContext.RequestAborted)
                : await _requestService.ApproveAsync(id, User.Identity?.Name, null, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                done++;
            }
            else
            {
                failed.AddRange(result.Errors);
            }
        }

        if (done > 0)
        {
            await _notifier.SuccessAsync(bulkAction == "Decline"
                ? H.Plural(done, "1 request has been declined.", "{0} requests have been declined.", done)
                : H.Plural(done, "1 request has been approved.", "{0} requests have been approved.", done));
        }

        foreach (var error in failed.Distinct(StringComparer.Ordinal))
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        return RedirectToAction(nameof(Index), new { teamId = model.TeamId, status = model.Status, workerId = model.WorkerId });
    }

    /// <summary>
    /// Shows the form that requests time off for a worker.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <returns>The form.</returns>
    [Admin("workforce/time-off/new", "WorkforceTimeOffCreate")]
    public async Task<IActionResult> Create(string workerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var tomorrow = (await PlanningOptions.TodayAsync(_localClock)).AddDays(1);
        var model = new TimeOffRequestEditViewModel
        {
            WorkerId = workerId,
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = "13:00",
            EndTime = "17:00",
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Checks or submits a request for a worker.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <param name="check">Whether to only show what the request would count and do.</param>
    /// <returns>A redirect to the request, or the form with what it would do or its problems.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/time-off/new", "WorkforceTimeOffCreate")]
    public async Task<IActionResult> CreatePost(TimeOffRequestEditViewModel model, bool check = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var worker = string.IsNullOrEmpty(model.WorkerId) ? null : await _workerManager.FindByIdAsync(model.WorkerId);

        if (worker is null)
        {
            ModelState.AddModelError(nameof(model.WorkerId), S["Choose the worker."]);
        }
        else if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (model.PartialDay)
        {
            // Part of a day is one day; the form turns the last day off.
            model.EndDate = model.StartDate;
        }

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.StartDate), S["Enter the first and the last day."]);
        }

        TimeOnly? startTime = null;
        TimeOnly? endTime = null;

        if (model.PartialDay)
        {
            if (PlanningInput.TryParseTime(model.StartTime, out var start) && PlanningInput.TryParseTime(model.EndTime, out var end))
            {
                startTime = start;
                endTime = end;
            }
            else
            {
                ModelState.AddModelError(nameof(model.StartTime), S["Enter when the time off starts and ends, such as 13:00 and 17:00."]);
            }
        }

        if (ModelState.IsValid)
        {
            var input = new TimeOffRequestInput
            {
                WorkerId = worker.ItemId,
                TypeId = model.TypeId,
                StartDate = model.StartDate.Value,
                EndDate = model.EndDate.Value,
                StartTime = startTime,
                EndTime = endTime,
                Reason = model.Reason,
            };

            if (check)
            {
                model.Preview = await _requestService.PreviewAsync(input, HttpContext.RequestAborted);
            }
            else
            {
                var result = await _requestService.SubmitAsync(input, User.Identity?.Name, HttpContext.RequestAborted);

                if (result.Succeeded)
                {
                    await _notifier.SuccessAsync(result.Value.Status switch
                    {
                        TimeOffRequestStatus.Approved => H["The time off is approved: it fits the allotment."],
                        TimeOffRequestStatus.Waitlisted => H["As many people as may be off already are on some of these days, so the request is on the waitlist."],
                        _ => H["The request has been submitted and waits for a decision."],
                    });

                    return RedirectToAction(nameof(Details), new { id = result.Value.ItemId });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
        }

        model.WorkerName = worker?.DisplayName;
        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Shows a request with the worker's balance, the team's other time off those days and the worker's shifts.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <returns>The request.</returns>
    [Admin("workforce/time-off/requests/{id}", "WorkforceTimeOffDetails")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var request = await _requestService.FindAsync(id, HttpContext.RequestAborted);

        if (request is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!scope.Covers(request.TeamId))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(request.WorkerId);
        var model = new TimeOffRequestDetailsViewModel
        {
            Request = request,
            Worker = worker,
            Type = await _typeManager.FindByIdAsync(request.TypeId),
            TeamName = (await _teamManager.FindByIdAsync(request.TeamId))?.Name,
            Balance = string.IsNullOrEmpty(request.PolicyId) ? null : await _balanceService.GetBalanceAsync(request.WorkerId, request.PolicyId, request.StartDate, HttpContext.RequestAborted),
            SubmittedByName = request.SubmittedBy,
            DecidedByName = request.DecidedBy,
        };

        var others = new List<TimeOffRequest>();

        foreach (var status in new TimeOffRequestStatus?[] { null, TimeOffRequestStatus.Approved })
        {
            var (_, items) = await _requestService.PageAsync(new TimeOffRequestQuery { TeamIds = [request.TeamId], Status = status, From = request.StartDate, To = request.EndDate }, 0, 50, HttpContext.RequestAborted);

            others.AddRange(items.Where(item => item.ItemId != request.ItemId));
        }

        model.OthersOff = await RowsAsync(others.OrderBy(item => item.StartDate).ToArray());

        if (worker is not null)
        {
            var fromUtc = CalendarTime.ToUtc(request.StartDate.ToDateTime(TimeOnly.MinValue), request.TimeZoneId);
            var toUtc = CalendarTime.ToUtc(request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), request.TimeZoneId);

            foreach (var shift in await _shiftService.ListWorkerShiftsAsync(worker.ItemId, fromUtc, toUtc, HttpContext.RequestAborted))
            {
                model.Shifts.Add((CalendarTime.ToLocal(shift.StartUtc, request.TimeZoneId), CalendarTime.ToLocal(shift.EndUtc, request.TimeZoneId), request.FlaggedShiftEventIds.Contains(shift.ItemId)));
            }
        }

        return View(model);
    }

    /// <summary>
    /// Approves a request.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="note">A note to the worker.</param>
    /// <returns>A redirect to the request.</returns>
    [HttpPost]
    [Admin("workforce/time-off/approve/{id}", "WorkforceTimeOffApprove")]
    public Task<IActionResult> Approve(string id, string note)
    {
        return DecideAsync(id, () => _requestService.ApproveAsync(id, User.Identity?.Name, note, HttpContext.RequestAborted), H["The time off has been approved."]);
    }

    /// <summary>
    /// Declines a request.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="note">Why.</param>
    /// <returns>A redirect to the request.</returns>
    [HttpPost]
    [Admin("workforce/time-off/decline/{id}", "WorkforceTimeOffDecline")]
    public Task<IActionResult> Decline(string id, string note)
    {
        return DecideAsync(id, () => _requestService.DeclineAsync(id, User.Identity?.Name, note, HttpContext.RequestAborted), H["The request has been declined."]);
    }

    /// <summary>
    /// Withdraws an open request or cancels approved time off.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="note">Why.</param>
    /// <returns>A redirect to the request.</returns>
    [HttpPost]
    [Admin("workforce/time-off/cancel/{id}", "WorkforceTimeOffCancel")]
    public Task<IActionResult> Cancel(string id, string note)
    {
        return DecideAsync(id, () => _requestService.CancelAsync(id, User.Identity?.Name, note, HttpContext.RequestAborted), H["The time off has been canceled and the balance given back."]);
    }

    /// <summary>
    /// Shows a worker's balances, ledger and own policies.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/time-off/workers/{workerId}", "WorkforceTimeOffWorker")]
    public async Task<IActionResult> Worker(string workerId, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var canApprove = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff);
        var canAdjust = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.AdjustTimeOffBalances);

        if (!canApprove && !canAdjust)
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var today = await PlanningOptions.TodayAsync(_localClock);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, entries) = await _balanceService.PageLedgerAsync(worker.ItemId, null, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var assigned = await _policyManager.ListAssignedAsync(worker.ItemId, worker.TeamMemberships.Select(membership => membership.TeamId), HttpContext.RequestAborted);
        var allPolicies = await _policyManager.GetAllAsync(HttpContext.RequestAborted);
        var types = (await _typeManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var balances = await _balanceService.GetBalancesAsync(worker.ItemId, today, HttpContext.RequestAborted);
        var model = new WorkerTimeOffViewModel
        {
            Worker = worker,
            Today = today,
            Balances = balances.ToList(),
            CanAdjust = canAdjust,
            CanAssign = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies),
            Assignments = allPolicies
                .SelectMany(policy => policy.Assignments.Where(assignment => assignment.WorkerId == worker.ItemId).Select(assignment => (policy, assignment)))
                .ToList(),
            PolicyOptions = assigned
                .Concat(balances.Select(balance => balance.Policy))
                .DistinctBy(policy => policy.ItemId)
                .OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(policy => new SelectListItem(policy.Name, policy.ItemId))
                .ToList(),
            AllPolicyOptions = allPolicies
                .Where(policy => policy.Enabled)
                .OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(policy => new SelectListItem(policy.Name, policy.ItemId))
                .ToList(),
        };

        model.Ledger = new AdminListContext<TimeOffLedgerRowViewModel>
        {
            Source = entries.Select(entry =>
            {
                var policy = allPolicies.FirstOrDefault(item => item.ItemId == entry.PolicyId);

                return new TimeOffLedgerRowViewModel
                {
                    Entry = entry,
                    PolicyName = policy?.Name,
                    Unit = policy is not null && types.TryGetValue(policy.TypeId ?? string.Empty, out var type) ? type.Unit : TimeOffUnit.Hours,
                };
            }).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "workerId", worker.ItemId } }),
        };

        return View(model);
    }

    /// <summary>
    /// Changes a worker's balance by hand.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="model">The change.</param>
    /// <returns>A redirect to the worker's time off.</returns>
    [HttpPost]
    [Admin("workforce/time-off/workers/{workerId}/adjust", "WorkforceTimeOffAdjust")]
    public async Task<IActionResult> Adjust(string workerId, TimeOffAdjustViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.AdjustTimeOffBalances))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _balanceService.AdjustAsync(new TimeOffAdjustment
        {
            WorkerId = worker.ItemId,
            PolicyId = model.PolicyId,
            Kind = Enum.TryParse<TimeOffLedgerKind>(model.Kind, out var kind) ? kind : TimeOffLedgerKind.Adjustment,
            Amount = model.Amount,
            EffectiveDate = model.EffectiveDate ?? await PlanningOptions.TodayAsync(_localClock),
            Note = model.Note,
        }, User.Identity?.Name, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The balance has been changed."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Worker), new { workerId = worker.ItemId });
    }

    /// <summary>
    /// Assigns a policy to the worker alone, which takes precedence over their teams' policies of its type.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="model">The policy and the dates.</param>
    /// <returns>A redirect to the worker's time off.</returns>
    [HttpPost]
    [Admin("workforce/time-off/workers/{workerId}/assign", "WorkforceTimeOffAssign")]
    public async Task<IActionResult> Assign(string workerId, TimeOffAssignViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId);
        var policy = string.IsNullOrEmpty(model.PolicyId) ? null : await _policyManager.FindByIdAsync(model.PolicyId);

        if (worker is null || policy is null)
        {
            return NotFound();
        }

        policy.Assignments.Add(new TimeOffPolicyAssignment { WorkerId = worker.ItemId, EffectiveFrom = model.EffectiveFrom, EffectiveTo = model.EffectiveTo });

        await SavePolicyAsync(policy, H["{0} now has the '{1}' policy.", worker.DisplayName, policy.Name]);

        return RedirectToAction(nameof(Worker), new { workerId = worker.ItemId });
    }

    /// <summary>
    /// Takes a policy assigned to the worker alone off them.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="policyId">The policy.</param>
    /// <returns>A redirect to the worker's time off.</returns>
    [HttpPost]
    [Admin("workforce/time-off/workers/{workerId}/unassign", "WorkforceTimeOffUnassign")]
    public async Task<IActionResult> Unassign(string workerId, string policyId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var policy = string.IsNullOrEmpty(policyId) ? null : await _policyManager.FindByIdAsync(policyId);

        if (policy is null)
        {
            return NotFound();
        }

        foreach (var assignment in policy.Assignments.Where(assignment => assignment.WorkerId == workerId).ToArray())
        {
            policy.Assignments.Remove(assignment);
        }

        await SavePolicyAsync(policy, H["The policy is no longer assigned to the worker alone."]);

        return RedirectToAction(nameof(Worker), new { workerId });
    }

    private async Task SavePolicyAsync(TimeOffPolicy policy, LocalizedHtmlString success)
    {
        var validation = await _policyManager.ValidateAsync(policy);

        if (validation.Succeeded)
        {
            await _policyManager.UpdateAsync(policy);
            await _notifier.SuccessAsync(success);
        }
        else
        {
            foreach (var error in validation.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error.ErrorMessage]);
            }
        }
    }

    private async Task<IActionResult> DecideAsync(string id, Func<Task<PlanningResult<TimeOffRequest>>> decide, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var request = await _requestService.FindAsync(id, HttpContext.RequestAborted);

        if (request is null)
        {
            return NotFound();
        }

        if (!(await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted)).Covers(request.TeamId))
        {
            return Forbid();
        }

        var result = await decide();

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<List<TimeOffRequestRowViewModel>> RowsAsync(IReadOnlyList<TimeOffRequest> requests)
    {
        var types = (await _typeManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var rows = new List<TimeOffRequestRowViewModel>();

        foreach (var request in requests)
        {
            rows.Add(new TimeOffRequestRowViewModel
            {
                Request = request,
                WorkerName = (await _workerManager.FindByIdAsync(request.WorkerId))?.DisplayName,
                TeamName = (await _teamManager.FindByIdAsync(request.TeamId))?.Name,
                Type = types.TryGetValue(request.TypeId ?? string.Empty, out var type) ? type : null,
            });
        }

        return rows;
    }

    private async Task PopulateAsync(TimeOffRequestEditViewModel model)
    {
        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var today = await PlanningOptions.TodayAsync(_localClock);
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

        foreach (var item in (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).Where(item => scope.Covers(item.Team.ItemId)))
        {
            foreach (var worker in await _workerManager.ListByTeamAsync(item.Team.ItemId, today, HttpContext.RequestAborted))
            {
                workers.TryAdd(worker.ItemId, worker);
            }
        }

        model.WorkerOptions = workers.Values
            .Where(worker => worker.Status != WorkerStatus.Terminated)
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == model.WorkerId))
            .ToList();
        model.TypeOptions = (await _typeManager.ListEnabledAsync(HttpContext.RequestAborted))
            .Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == model.TypeId))
            .ToList();
    }
}
