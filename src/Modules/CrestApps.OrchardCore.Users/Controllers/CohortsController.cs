using System.Security.Claims;
using CrestApps.OrchardCore.Users.Cohorts;
using CrestApps.OrchardCore.Users.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// The cohorts: create, rename and delete them, and add and remove their people, one at a time or many at once.
/// </summary>
public sealed class CohortsController : Controller
{
    private const int MaxBulkLines = 2_000;

    private readonly IAuthorizationService _authorizationService;
    private readonly ICohortService _cohorts;
    private readonly INotifier _notifier;
    private readonly IDisplayManager<CohortEditViewModel> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohortsController"/> class.
    /// </summary>
    public CohortsController(
        IAuthorizationService authorizationService,
        ICohortService cohorts,
        INotifier notifier,
        IDisplayManager<CohortEditViewModel> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<CohortsController> htmlLocalizer,
        IStringLocalizer<CohortsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _cohorts = cohorts;
        _notifier = notifier;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the cohorts.
    /// </summary>
    /// <param name="search">A part of a name to find.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("cohorts", "Cohorts")]
    public async Task<IActionResult> Index(
        string search,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, cohorts) = await _cohorts.PageAsync(search, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<CohortRow>();

        foreach (var cohort in cohorts)
        {
            rows.Add(new CohortRow
            {
                CohortId = cohort.CohortId,
                Name = cohort.Name,
                Description = cohort.Description,
                MemberCount = await _cohorts.CountMembersAsync(cohort.CohortId, HttpContext.RequestAborted),
            });
        }

        return View(new CohortListViewModel
        {
            Search = search,
            List = new AdminListContext<CohortRow>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "search", search } }),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing cohort.
    /// </summary>
    /// <param name="id">The cohort, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("cohorts/edit/{id?}", "CohortsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        var model = new CohortEditViewModel();

        if (!string.IsNullOrEmpty(id))
        {
            var cohort = await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted);

            if (cohort is null)
            {
                return NotFound();
            }

            model = new CohortEditViewModel { CohortId = cohort.CohortId, Name = cohort.Name, Description = cohort.Description };
        }

        model.Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: string.IsNullOrEmpty(id));

        return View(model);
    }

    /// <summary>
    /// Creates or renames a cohort.
    /// </summary>
    /// <param name="id">The cohort, or empty for a new one.</param>
    /// <param name="model">The fields.</param>
    /// <returns>The members of a new cohort, the list after a rename, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("cohorts/edit/{id?}", "CohortsEdit")]
    public async Task<IActionResult> EditPost(string id, CohortEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        model.CohortId = id;

        if (!string.IsNullOrEmpty(id) && await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted) is null)
        {
            return NotFound();
        }

        model.Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: string.IsNullOrEmpty(id));

        var result = string.IsNullOrEmpty(id)
            ? await _cohorts.CreateAsync(model.Name, model.Description, cancellationToken: HttpContext.RequestAborted)
            : await _cohorts.UpdateAsync(id, model.Name, model.Description, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(model.Name), error);
            }

            return View(model);
        }

        await _notifier.SuccessAsync(H["The cohort has been saved."]);

        // A new cohort is empty: its people come next.
        return string.IsNullOrEmpty(id)
            ? RedirectToAction(nameof(Members), new { id = result.Cohort.CohortId })
            : RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a cohort nothing uses.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("cohorts/delete/{id}", "CohortsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        if (await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted) is null)
        {
            return NotFound();
        }

        var errors = await _cohorts.DeleteAsync(id, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", errors)]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The cohort has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lists the people in a cohort, with the form that adds more.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProvider">The display name provider.</param>
    /// <returns>The page.</returns>
    [Admin("cohorts/members/{id}", "CohortsMembers")]
    public async Task<IActionResult> Members(
        string id,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] UserManager<IUser> userManager,
        [FromServices] IDisplayNameProvider displayNameProvider)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        var cohort = await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted);

        if (cohort is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, members) = await _cohorts.PageMembersAsync(cohort.CohortId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<CohortMemberRow>();

        foreach (var member in members)
        {
            var user = await userManager.FindByIdAsync(member.UserId);

            rows.Add(new CohortMemberRow
            {
                CohortId = cohort.CohortId,
                UserId = member.UserId,
                UserName = user?.UserName,
                DisplayName = user is null ? member.UserId : await displayNameProvider.GetAsync(user, HttpContext.RequestAborted),
                Email = user is null ? null : await userManager.GetEmailAsync(user),
                AddedUtc = member.AddedUtc,
            });
        }

        return View(new CohortMembersViewModel
        {
            CohortId = cohort.CohortId,
            Name = cohort.Name,
            Count = count,
            List = new AdminListContext<CohortMemberRow>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "id", cohort.CohortId } }),
            },
        });
    }

    /// <summary>
    /// Adds the people chosen in the picker and the ones named one per line, by user name or e-mail.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <param name="model">The people.</param>
    /// <param name="userManager">The user manager.</param>
    /// <returns>A redirect to the members.</returns>
    [HttpPost]
    [Admin("cohorts/members/{id}/add", "CohortsAddMembers")]
    public async Task<IActionResult> AddMembers(string id, CohortAddMembersViewModel model, [FromServices] UserManager<IUser> userManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        var cohort = await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted);

        if (cohort is null)
        {
            return NotFound();
        }

        var userIds = new List<string>(model.UserIds?.Where(value => !string.IsNullOrWhiteSpace(value)) ?? []);
        var unknown = new List<string>();
        var lines = (model.People ?? string.Empty)
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (lines.Length > MaxBulkLines)
        {
            await _notifier.ErrorAsync(H["Add at most {0} people at once.", MaxBulkLines]);

            return RedirectToAction(nameof(Members), new { id });
        }

        foreach (var line in lines)
        {
            var user = await userManager.FindByNameAsync(line) ?? (line.Contains('@') ? await userManager.FindByEmailAsync(line) : null);

            if (user is null)
            {
                unknown.Add(line);

                continue;
            }

            userIds.Add(await userManager.GetUserIdAsync(user));
        }

        var added = await _cohorts.AddMembersAsync(cohort.CohortId, userIds, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        await _notifier.SuccessAsync(added.Count == 1 ? H["1 person was added."] : H["{0} people were added.", added.Count]);

        if (unknown.Count > 0)
        {
            await _notifier.WarningAsync(H["No user has the name or e-mail {0}.", string.Join(", ", unknown.Take(20))]);
        }

        return RedirectToAction(nameof(Members), new { id });
    }

    /// <summary>
    /// Removes a person from a cohort.
    /// </summary>
    /// <param name="id">The cohort.</param>
    /// <param name="userId">The person.</param>
    /// <returns>A redirect to the members.</returns>
    [HttpPost]
    [Admin("cohorts/members/{id}/remove/{userId}", "CohortsRemoveMember")]
    public async Task<IActionResult> RemoveMember(string id, string userId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageCohorts))
        {
            return Forbid();
        }

        if (await _cohorts.FindByIdAsync(id, HttpContext.RequestAborted) is null)
        {
            return NotFound();
        }

        var removed = await _cohorts.RemoveMembersAsync(id, [userId], User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);

        if (removed.Count > 0)
        {
            await _notifier.SuccessAsync(H["The person has been removed from the cohort."]);
        }

        return RedirectToAction(nameof(Members), new { id });
    }
}
