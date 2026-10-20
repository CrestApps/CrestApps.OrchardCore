using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Classifieds.Controllers;

/// <summary>
/// Content › Posted listings: approving and rejecting what visitors post, and every listing's status.
/// </summary>
[Admin("classifieds/{action}/{contentItemId?}", "Classifieds{action}")]
public sealed class ClassifiedsAdminController : Controller
{
    private readonly ClassifiedService _classifieds;
    private readonly IContentManager _contentManager;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ClassifiedsAdminController(
        ClassifiedService classifieds,
        IContentManager contentManager,
        IUserService userService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<ClassifiedsAdminController> htmlLocalizer,
        IStringLocalizer<ClassifiedsAdminController> stringLocalizer)
    {
        _classifieds = classifieds;
        _contentManager = contentManager;
        _userService = userService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("classifieds", "Classifieds")]
    public async Task<IActionResult> Index(string q, ClassifiedStatus? status, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ClassifiedsPermissions.ModerateClassifieds))
        {
            return Forbid();
        }

        // What waits for a moderator comes first.
        status ??= Request.Query.ContainsKey("status") ? null : ClassifiedStatus.Pending;

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _classifieds.PageAsync(status, q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var entries = new List<ClassifiedEntry>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.Owner) && !names.ContainsKey(item.Owner))
            {
                names[item.Owner] = (await _userService.GetUserByUniqueIdAsync(item.Owner))?.UserName ?? item.Author;
            }

            entries.Add(new ClassifiedEntry
            {
                ContentItemId = item.ContentItemId,
                ContentType = item.ContentType,
                Title = item.DisplayText,
                OwnerName = item.Owner is null ? item.Author : names[item.Owner],
                Part = item.TryGet<ClassifiedPart>(out var part) ? part : new ClassifiedPart(),
            });
        }

        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        routeData.Values.TryAdd("status", status?.ToString() ?? string.Empty);

        var model = new ClassifiedsAdminIndexViewModel
        {
            Search = q,
            Status = status,
            List = new AdminListContext<ClassifiedEntry>
            {
                Source = entries,
                Pager = (IShape)await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        model.List.ToolbarActions = await _shapeFactory.CreateAsync("ClassifiedsAdminListFilters", Arguments.From(new
        {
            Statuses = new List<SelectListItem>
            {
                new(S["Waiting for approval"], nameof(ClassifiedStatus.Pending), status == ClassifiedStatus.Pending),
                new(S["Up"], nameof(ClassifiedStatus.Approved), status == ClassifiedStatus.Approved),
                new(S["Rejected"], nameof(ClassifiedStatus.Rejected), status == ClassifiedStatus.Rejected),
                new(S["Expired"], nameof(ClassifiedStatus.Expired), status == ClassifiedStatus.Expired),
                new(S["Every listing"], string.Empty, status is null),
            },
        }));

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ClassifiedsPermissions.ModerateClassifieds))
        {
            return Forbid();
        }

        if (await _contentManager.GetAsync(contentItemId, VersionOptions.Latest) is not { } item || !item.Has<ClassifiedPart>())
        {
            return NotFound();
        }

        await _classifieds.ApproveAsync(item);
        await _notifier.SuccessAsync(H["{0} is up.", item.DisplayText]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(string contentItemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ClassifiedsPermissions.ModerateClassifieds))
        {
            return Forbid();
        }

        if (await _contentManager.GetAsync(contentItemId, VersionOptions.Latest) is not { } item || !item.Has<ClassifiedPart>())
        {
            return NotFound();
        }

        await _classifieds.RejectAsync(item, reason);
        await _notifier.SuccessAsync(H["{0} was rejected, and its poster was told.", item.DisplayText]);

        return RedirectToAction(nameof(Index));
    }
}
