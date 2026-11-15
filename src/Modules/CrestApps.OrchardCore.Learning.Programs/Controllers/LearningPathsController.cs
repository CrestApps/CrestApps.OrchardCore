using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Programs.Controllers;

/// <summary>
/// The people on a learning path, and starting on one: by staff from the admin, or by the learner from the path's page.
/// </summary>
public sealed class LearningPathsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILearningPathService _paths;
    private readonly IContentManager _contentManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public LearningPathsController(
        IAuthorizationService authorizationService,
        ILearningPathService paths,
        IContentManager contentManager,
        INotifier notifier,
        IHtmlLocalizer<LearningPathsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _paths = paths;
        _contentManager = contentManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the people on a path.
    /// </summary>
    [Admin("learning/paths/{id}/people", "LearningPathPeople")]
    public async Task<IActionResult> People(string id, LearningPathState? state, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var path = await _contentManager.GetAsync(id, VersionOptions.Latest);

        if (path is null || !path.Has<LearningPathPart>())
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, path))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _paths.PageAsync(id, state, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeValues = new RouteValueDictionary { { "id", id } };

        if (state is not null)
        {
            routeValues["state"] = state.ToString();
        }

        return View(new LearningPathPeopleViewModel
        {
            PathId = id,
            PathTitle = path.DisplayText,
            State = state,
            List = new AdminListContext<LearningPathProgress>
            {
                Source = items.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, routeValues),
            },
        });
    }

    /// <summary>
    /// Starts a person on a path from the admin.
    /// </summary>
    [HttpPost]
    [Admin("learning/paths/{id}/start", "LearningPathStart")]
    public async Task<IActionResult> Start(string id, string userName, [FromServices] LearnerUserLookup users)
    {
        var path = await _contentManager.GetAsync(id, VersionOptions.Published);

        if (path is null || !path.Has<LearningPathPart>())
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, path))
        {
            return Forbid();
        }

        var userId = string.IsNullOrWhiteSpace(userName) ? null : await users.FindUserIdAsync(userName.Trim());

        if (userId is null)
        {
            await _notifier.ErrorAsync(H["No user is named '{0}'.", userName ?? string.Empty]);

            return RedirectToAction(nameof(People), new { id });
        }

        var result = await _paths.StartAsync(new LearningPathStartRequest { PathId = id, UserId = userId, Actor = User.Identity?.Name, Source = ProgramsConstants.PathSource }, HttpContext.RequestAborted);

        if (result.Progress is null)
        {
            await _notifier.ErrorAsync(H["{0}", result.Error ?? string.Empty]);
        }
        else
        {
            await _notifier.SuccessAsync(result.Created ? H["'{0}' is on the path.", userName] : H["'{0}' was already on the path.", userName]);
        }

        return RedirectToAction(nameof(People), new { id });
    }

    /// <summary>
    /// Starts the signed-in person on a path that allows it, from the path's page.
    /// </summary>
    [HttpPost]
    [Route("learning/paths/{id}/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSelf(string id, string returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var path = await _contentManager.GetAsync(id, VersionOptions.Published);

        if (path is null || !path.TryGet<LearningPathPart>(out var part) || !part.AllowSelfStart || !await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent, path))
        {
            return NotFound();
        }

        var result = await _paths.StartAsync(new LearningPathStartRequest { PathId = id, UserId = userId, Actor = User.Identity?.Name, Source = ProgramsConstants.PathSource }, HttpContext.RequestAborted);

        if (result.Progress is null)
        {
            await _notifier.ErrorAsync(H["{0}", result.Error ?? string.Empty]);
        }
        else
        {
            await _notifier.SuccessAsync(H["You are on the path. Its first courses are in My courses."]);
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("~/learning/required");
    }
}
