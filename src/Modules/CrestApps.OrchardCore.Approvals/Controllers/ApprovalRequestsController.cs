using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Approvals.ViewModels;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Approvals.Controllers;

/// <summary>
/// Approvals &gt; Requests. Three lists: the requests waiting for the reader's decision (anyone a policy names, with no
/// Approvals permission), the requests the reader made, and every request the reader may see (with the View approval
/// requests permission and the subject type's access). A request opens and is decided by its approvers, opens for its
/// requester, who may withdraw it, and opens for those who see it through the permission.
/// </summary>
[Admin]
public sealed class ApprovalRequestsController : Controller
{
    private readonly IApprovalRequestManager _manager;
    private readonly IApprovalRequestStore _store;
    private readonly IApprovalService _approvalService;
    private readonly IApprovalTypeCatalog _types;
    private readonly ApprovalInbox _inbox;
    private readonly ISourceAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ApprovalRequest> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestsController"/> class.
    /// </summary>
    public ApprovalRequestsController(
        IApprovalRequestManager manager,
        IApprovalRequestStore store,
        IApprovalService approvalService,
        IApprovalTypeCatalog types,
        ApprovalInbox inbox,
        ISourceAccessService access,
        IAuthorizationService authorizationService,
        IDisplayManager<ApprovalRequest> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizerFactory localizerFactory,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<ApprovalRequestsController> htmlLocalizer,
        IStringLocalizer<ApprovalRequestsController> stringLocalizer)
    {
        _manager = manager;
        _store = store;
        _approvalService = approvalService;
        _types = types;
        _inbox = inbox;
        _access = access;
        _authorizationService = authorizationService;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _localizerFactory = localizerFactory;
        _notifier = notifier;
        _localClock = localClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists requests.
    /// </summary>
    [Admin("approvals", "ApprovalRequestsIndex")]
    public async Task<IActionResult> Index(
        ApprovalRequestsIndexViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        filter.CanSeeAll = await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests);

        // Who can't see every request has their inbox and their own requests.
        if (filter.View is not (ApprovalRequestsIndexViewModel.WaitingView or ApprovalRequestsIndexViewModel.AllView or ApprovalRequestsIndexViewModel.MineView)
            || (filter.View == ApprovalRequestsIndexViewModel.AllView && !filter.CanSeeAll))
        {
            filter.View = filter.CanSeeAll ? ApprovalRequestsIndexViewModel.AllView : ApprovalRequestsIndexViewModel.WaitingView;
        }

        filter.Status ??= nameof(ApprovalStatus.Pending);
        filter.Type = _types.Find(filter.Type)?.Name;

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        ApprovalStatus? status = Enum.TryParse<ApprovalStatus>(filter.Status, out var parsed) ? parsed : null;
        int count;
        IReadOnlyList<ApprovalRequest> requests;

        if (filter.View == ApprovalRequestsIndexViewModel.WaitingView)
        {
            (count, requests) = await _inbox.ListWaitingAsync(User, filter.Type, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        }
        else
        {
            (count, requests) = await _store.ListPageAsync(
                new ApprovalRequestQuery
                {
                    Status = status,
                    Type = filter.Type,
                    OldestFirst = status == ApprovalStatus.Pending,
                    RequesterUserId = filter.View == ApprovalRequestsIndexViewModel.MineView ? UserId() ?? "-" : null,
                    Access = filter.View == ApprovalRequestsIndexViewModel.MineView
                        ? SourceAccessFilter.Unrestricted
                        : await _access.GetFilterAsync(User, SourceAccessLevel.View, HttpContext.RequestAborted),
                },
                pager.GetStartIndex(),
                pager.PageSize,
                HttpContext.RequestAborted);
        }

        filter.TypeOptions = [new SelectListItem(S["All types"], string.Empty, string.IsNullOrEmpty(filter.Type)), .. _types.List()
            .Select(type => new SelectListItem(TypeName(type), type.Name, string.Equals(type.Name, filter.Type, StringComparison.OrdinalIgnoreCase)))];

        var routeData = new RouteData();
        routeData.Values["View"] = filter.View;
        routeData.Values["Status"] = filter.Status;
        routeData.Values["Type"] = filter.Type;

        filter.List = new AdminListContext<ApprovalRequest>
        {
            Source = requests,
            Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
        };

        return View(filter);
    }

    /// <summary>
    /// Shows a request, with the decision form for its approvers.
    /// </summary>
    [Admin("approvals/decide/{id}", "ApprovalRequestsDecide")]
    public async Task<IActionResult> Decide(string id)
    {
        if (await FindAsync(id, SourceAccessLevel.View) is not { } request)
        {
            return NotFound();
        }

        return View(await BuildAsync(new ApprovalDecideViewModel(), request));
    }

    /// <summary>
    /// Records the reader's decision.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Decide))]
    [Admin("approvals/decide/{id}", "ApprovalRequestsDecide")]
    public async Task<IActionResult> DecidePost(string id, ApprovalDecideViewModel model, [FromForm(Name = "decision")] string decision)
    {
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

        var stage = request.GetCurrentStage();

        await _notifier.SuccessAsync(request.Status switch
        {
            ApprovalStatus.Approved => H["Approved. The request has the approvals it needs."],
            ApprovalStatus.Rejected => H["Rejected. The requester is told why."],
            _ when request.StageApprovalCount == 0 => H["Your approval is recorded. The request moves to stage {0}: {1}.", request.CurrentStage + 1, stage.Title ?? string.Empty],
            _ => H["Your approval is recorded. It needs {0} more.", stage.RequiredApprovals - request.StageApprovalCount],
        });

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Withdraws a pending request: its requester, or someone who manages requests.
    /// </summary>
    [HttpPost]
    [Admin("approvals/cancel/{id}", "ApprovalRequestsCancel")]
    public async Task<IActionResult> Cancel(string id, string reason)
    {
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

    // A request opens for those who may decide it, for its requester, and for those who see it through the permission
    // and the subject type's access. Anyone else gets nothing, not even that it exists.
    private async Task<ApprovalRequest> FindAsync(string id, SourceAccessLevel level)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } request)
        {
            return null;
        }

        if (request.RequesterUserId is { } requester && requester == UserId())
        {
            return request;
        }

        if ((await _approvalService.GetEligibilityAsync(request, User, HttpContext.RequestAborted)).CanDecide)
        {
            return request;
        }

        return await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ViewApprovalRequests)
            && await _access.CanAccessAsync(User, request.SubjectKind, request.AccessScope, level, HttpContext.RequestAborted)
            ? request
            : null;
    }

    // The requester withdraws their own request; managers of requests withdraw any they may manage.
    private async Task<bool> CanCancelAsync(ApprovalRequest request)
        => request.Status == ApprovalStatus.Pending
            && ((request.RequesterUserId is { } requester && requester == UserId())
                || (await _authorizationService.AuthorizeAsync(User, ApprovalsPermissions.ManageApprovalRequests)
                    && await _access.CanAccessAsync(User, request.SubjectKind, request.AccessScope, SourceAccessLevel.Manage, HttpContext.RequestAborted)));

    private async Task<ApprovalDecideViewModel> BuildAsync(ApprovalDecideViewModel model, ApprovalRequest request)
    {
        var handler = _types.GetHandler(request.SubjectKind);
        var type = _types.Find(request.SubjectKind);

        model.Request = request;
        model.TypeDisplayName = type is null ? request.SubjectKind : TypeName(type);
        model.SubjectDisplay = await handler.GetSubjectDisplayAsync(request, HttpContext.RequestAborted);
        model.SubjectUrl = await handler.GetSubjectUrlAsync(request, HttpContext.RequestAborted);
        model.Display = await _displayManager.BuildDisplayAsync(request, _updateModelAccessor.ModelUpdater, "DetailAdmin");
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

    private string TypeName(ApprovalType type)
        => type.DisplayName is null ? type.Name : _localizerFactory.Localize(type.DisplayName).Value;

    private string UserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private Task<DateTimeOffset> ToLocalAsync(DateTime utc)
        => _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
}
