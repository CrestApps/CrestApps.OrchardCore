using System.Security.Claims;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Memberships.ViewModels;
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
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Memberships.Controllers;

/// <summary>
/// Commerce › Memberships: who's a member of what, until when, and ending one early.
/// </summary>
[Admin("memberships/{action}/{itemId?}", "Memberships{action}")]
public sealed class MembershipsAdminController : Controller
{
    private readonly IMembershipStore _memberships;
    private readonly MembershipService _service;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public MembershipsAdminController(
        IMembershipStore memberships,
        MembershipService service,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<MembershipsAdminController> htmlLocalizer,
        IStringLocalizer<MembershipsAdminController> stringLocalizer)
    {
        _memberships = memberships;
        _service = service;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("memberships", "Memberships")]
    public async Task<IActionResult> Index(string q, MembershipStatus? status, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MembershipsPermissions.ManageMemberships))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, memberships) = await _memberships.PageAsync(q, status, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        if (status is not null)
        {
            routeData.Values.TryAdd("status", status.ToString());
        }

        var model = new MembershipsIndexViewModel
        {
            Search = q,
            Status = status,
            List = new AdminListContext<Membership>
            {
                Source = memberships,
                Pager = (IShape)await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        // The status filter renders at the end of the list's toolbar.
        var statuses = new List<SelectListItem>
        {
            new(S["Every membership"], string.Empty, status is null),
            new(S["Active"], nameof(MembershipStatus.Active), status == MembershipStatus.Active),
            new(S["Expired"], nameof(MembershipStatus.Expired), status == MembershipStatus.Expired),
            new(S["Ended early"], nameof(MembershipStatus.Revoked), status == MembershipStatus.Revoked),
        };

        model.List.ToolbarActions = await _shapeFactory.CreateAdminListFiltersAsync("Memberships", "memberships-filter", new AdminListFilter("status", statuses, S["Filter by status"], S["Status"]));

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> End(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MembershipsPermissions.ManageMemberships))
        {
            return Forbid();
        }

        if (await _memberships.FindByIdAsync(itemId) is not { Status: MembershipStatus.Active } membership)
        {
            return NotFound();
        }

        await _service.EndAsync(membership, MembershipStatus.Revoked, S["Ended by {0}.", User.Identity?.Name], HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The membership was ended, and its member taken out of its roles."]);

        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// The signed-in customer's memberships, on the storefront account. The list is the <c>AccountMemberships</c> shape,
/// so a theme can override it.
/// </summary>
[Authorize]
public sealed class AccountMembershipsController : Controller
{
    private readonly IMembershipStore _memberships;
    private readonly IShapeFactory _shapeFactory;

    public AccountMembershipsController(IMembershipStore memberships, IShapeFactory shapeFactory)
    {
        _memberships = memberships;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/memberships", Name = "AccountMemberships")]
    public async Task<IActionResult> Index()
    {
        var memberships = await _memberships.ListByUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<AccountMembershipsViewModel>("AccountMemberships", model =>
        {
            model.Memberships = [.. memberships.OrderBy(membership => membership.Status).ThenByDescending(membership => membership.CreatedUtc)];
        }));
    }
}
