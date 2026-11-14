using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Approvals.ViewModels;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Approvals.Controllers;

/// <summary>
/// Approvals › Requests: the requests a person may see, and the page where an approver decides one.
/// </summary>
[Admin]
public sealed class ApprovalRequestsController : Controller
{
    private readonly IApprovalRequestManager _manager;
    private readonly IApprovalRequestStore _store;
    private readonly IApprovalService _approvalService;
    private readonly ISourceAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;

    public ApprovalRequestsController(
        IApprovalRequestManager manager,
        IApprovalRequestStore store,
        IApprovalService approvalService,
        ISourceAccessService access,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<ApprovalRequestsController> htmlLocalizer)
    {
        _manager = manager;
        _store = store;
        _approvalService = approvalService;
        _access = access;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _localClock = localClock;
        H = htmlLocalizer;
    }

    [Admin("approvals", "ApprovalRequestsIndex")]
    public async Task<IActionResult> Index(
        ApprovalRequestsIndexViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests))
        {
            return Forbid();
        }

        filter.Status ??= nameof(ApprovalStatus.Pending);

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        ApprovalStatus? status = Enum.TryParse<ApprovalStatus>(filter.Status, out var parsed) ? parsed : null;
        var (count, requests) = await _store.ListPageAsync(
            new ApprovalRequestQuery
            {
                Status = status,
                OldestFirst = status == ApprovalStatus.Pending,
                Access = await _access.GetFilterAsync(User, SourceAccessLevel.View, HttpContext.RequestAborted),
            },
            pager.GetStartIndex(),
            pager.PageSize,
            HttpContext.RequestAborted);
        var routeData = new RouteData();

        routeData.Values["Status"] = filter.Status;

        filter.List = new AdminListContext<ApprovalRequest>
        {
            Source = requests,
            Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
        };

        return View(filter);
    }

    [Admin("approvals/decide/{id}", "ApprovalRequestsDecide")]
    public async Task<IActionResult> Decide(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests))
        {
            return Forbid();
        }

        if (await FindAsync(id, SourceAccessLevel.View) is not { } request)
        {
            return NotFound();
        }

        return View(await BuildAsync(new ApprovalDecideViewModel(), request));
    }

    [HttpPost]
    [ActionName(nameof(Decide))]
    [Admin("approvals/decide/{id}", "ApprovalRequestsDecide")]
    public async Task<IActionResult> DecidePost(string id, ApprovalDecideViewModel model, [FromForm(Name = "decision")] string decision)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests))
        {
            return Forbid();
        }

        if (await FindAsync(id, SourceAccessLevel.Manage) is not { } request)
        {
            return NotFound();
        }

        if (!Enum.TryParse<ApprovalDecisionKind>(decision, out var kind))
        {
            return BadRequest();
        }

        var result = await _approvalService.DecideAsync(request, User, kind, model.Note, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(await BuildAsync(model, request));
        }

        await _notifier.SuccessAsync(request.Status switch
        {
            ApprovalStatus.Approved => H["Approved. The request has the approvals it needs."],
            ApprovalStatus.Rejected => H["Rejected. The requester is told why."],
            _ => H["Your approval is recorded. It needs {0} more.", request.RequiredApprovals - request.ApprovalCount],
        });

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("approvals/cancel/{id}", "ApprovalRequestsCancel")]
    public async Task<IActionResult> Cancel(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests))
        {
            return Forbid();
        }

        if (await FindAsync(id, SourceAccessLevel.Manage) is not { } request)
        {
            return NotFound();
        }

        if (!await CanCancelAsync(request))
        {
            return Forbid();
        }

        if (await _approvalService.CancelAsync(request, reason, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The request was withdrawn."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The request is already decided."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ApprovalRequest> FindAsync(string id, SourceAccessLevel level)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } request)
        {
            return null;
        }

        return await _access.CanAccessAsync(User, request.SubjectKind, request.AccessScope, level, HttpContext.RequestAborted) ? request : null;
    }

    // The requester withdraws their own request; managers of requests withdraw any.
    private async Task<bool> CanCancelAsync(ApprovalRequest request)
        => request.Status == ApprovalStatus.Pending
            && (request.RequesterUserId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                || await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ManageApprovalRequests));

    private async Task<ApprovalDecideViewModel> BuildAsync(ApprovalDecideViewModel model, ApprovalRequest request)
    {
        model.Request = request;
        model.Eligibility = await _approvalService.GetEligibilityAsync(request, User, HttpContext.RequestAborted);
        model.CanCancel = await CanCancelAsync(request);
        model.CreatedLocal = await ToLocalAsync(request.CreatedUtc);
        model.DueLocal = request.DueUtc is { } due ? await ToLocalAsync(due) : null;
        model.DecisionTimes = [];

        foreach (var decision in request.Decisions)
        {
            model.DecisionTimes.Add(await ToLocalAsync(decision.DecidedUtc));
        }

        return model;
    }

    private Task<DateTimeOffset> ToLocalAsync(DateTime utc)
        => _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
}
