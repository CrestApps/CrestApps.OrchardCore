using CrestApps.OrchardCore.Users.Audiences;
using CrestApps.OrchardCore.Users.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Controllers;

/// <summary>
/// The audiences administrators define, the ones modules define, and the people in each one.
/// </summary>
public sealed class UserAudiencesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserAudienceService _audiences;
    private readonly IEnumerable<IUserAudienceRuleSource> _sources;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAudiencesController"/> class.
    /// </summary>
    public UserAudiencesController(
        IAuthorizationService authorizationService,
        IUserAudienceService audiences,
        IEnumerable<IUserAudienceRuleSource> sources,
        INotifier notifier,
        IHtmlLocalizer<UserAudiencesController> htmlLocalizer,
        IStringLocalizer<UserAudiencesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _audiences = audiences;
        _sources = sources;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the audiences.
    /// </summary>
    /// <param name="search">A part of a name to find.</param>
    /// <returns>The page.</returns>
    [Admin("user-audiences", "UserAudiences")]
    public async Task<IActionResult> Index(string search)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageUserAudiences))
        {
            return Forbid();
        }

        var rows = new List<UserAudienceRow>();

        foreach (var audience in await _audiences.ListAsync(HttpContext.RequestAborted))
        {
            if (!string.IsNullOrWhiteSpace(search)
                && audience.DisplayName?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) != true
                && audience.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            var definition = audience.IsDefined ? await _audiences.FindDefinitionAsync(audience.Name, HttpContext.RequestAborted) : null;

            rows.Add(new UserAudienceRow
            {
                Name = audience.Name,
                DisplayName = audience.DisplayName,
                Description = definition?.Description,
                IsDefined = audience.IsDefined,
                RuleCount = definition?.Rules.Count ?? 0,
                MatchAll = definition?.Join != UserAudienceJoin.Any,
            });
        }

        return View(new UserAudienceListViewModel
        {
            Search = search,
            List = new AdminListContext<UserAudienceRow> { Source = rows },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing audience.
    /// </summary>
    /// <param name="name">The audience, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("user-audiences/edit/{name?}", "UserAudiencesEdit")]
    public async Task<IActionResult> Edit(string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageUserAudiences))
        {
            return Forbid();
        }

        var model = new UserAudienceEditViewModel { IsNew = true };

        if (!string.IsNullOrEmpty(name))
        {
            var definition = await _audiences.FindDefinitionAsync(name, HttpContext.RequestAborted);

            if (definition is null)
            {
                return NotFound();
            }

            model = new UserAudienceEditViewModel
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                Join = definition.Join,
                Rules = definition.Rules.Select(rule => new UserAudienceRuleEditViewModel { Source = rule.Source, Values = rule.Values.ToList(), Number = rule.Number }).ToList(),
            };
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves an audience.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("user-audiences/edit/{name?}", "UserAudiencesEdit")]
    public async Task<IActionResult> EditPost(UserAudienceEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageUserAudiences))
        {
            return Forbid();
        }

        if (!model.IsNew && await _audiences.FindDefinitionAsync(model.Name, HttpContext.RequestAborted) is null)
        {
            return NotFound();
        }

        if (model.IsNew && await _audiences.FindDefinitionAsync(model.Name, HttpContext.RequestAborted) is not null)
        {
            ModelState.AddModelError(nameof(model.Name), S["Another audience has this technical name."]);
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            ModelState.AddModelError(nameof(model.DisplayName), S["The name is required."]);
        }

        var definition = new UserAudienceDefinition
        {
            Name = model.Name,
            DisplayName = model.DisplayName?.Trim(),
            Description = model.Description?.Trim(),
            Join = model.Join,
            Rules = model.Rules
                .Where(rule => !string.IsNullOrEmpty(rule.Source))
                .Select(rule => new UserAudienceRule
                {
                    Source = rule.Source,
                    Values = rule.Values?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList() ?? [],
                    Number = rule.Number,
                })
                .ToList(),
        };

        if (ModelState.IsValid)
        {
            var errors = await _audiences.SaveDefinitionAsync(definition, HttpContext.RequestAborted);

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (errors.Count == 0)
            {
                await _notifier.SuccessAsync(H["The audience has been saved."]);

                return RedirectToAction(nameof(Index));
            }
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Deletes an audience an administrator defined.
    /// </summary>
    /// <param name="name">The audience.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("user-audiences/delete/{name}", "UserAudiencesDelete")]
    public async Task<IActionResult> Delete(string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageUserAudiences))
        {
            return Forbid();
        }

        // An audience something uses stays: deleting it would make, for example, a required learning rule release
        // everyone and require them again with new due dates when it comes back.
        var uses = new List<string>();

        foreach (var usage in HttpContext.RequestServices.GetServices<IUserAudienceUsage>())
        {
            uses.AddRange(await usage.ListUsesAsync(name, HttpContext.RequestAborted));
        }

        if (uses.Count > 0)
        {
            await _notifier.ErrorAsync(H["The audience is used by {0}. Change those first.", string.Join(", ", uses)]);

            return RedirectToAction(nameof(Index));
        }

        if (!await _audiences.DeleteDefinitionAsync(name, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(H["The audience has been deleted."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lists the people in an audience, so its rules can be checked before other features target it.
    /// </summary>
    /// <param name="name">The audience.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProvider">The display name provider.</param>
    /// <returns>The page.</returns>
    [Admin("user-audiences/members/{name}", "UserAudiencesMembers")]
    public async Task<IActionResult> Members(
        string name,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] UserManager<IUser> userManager,
        [FromServices] IDisplayNameProvider displayNameProvider)
    {
        if (!await _authorizationService.AuthorizeAsync(User, UserPermissions.ManageUserAudiences))
        {
            return Forbid();
        }

        var audience = (await _audiences.ListAsync(HttpContext.RequestAborted)).FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        if (audience is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, userIds) = await _audiences.GetMembersAsync(audience.Name, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<UserAudienceMemberRow>();

        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId);

            rows.Add(user is null
                ? new UserAudienceMemberRow { UserId = userId, DisplayName = userId }
                : new UserAudienceMemberRow
                {
                    UserId = userId,
                    UserName = user.UserName,
                    DisplayName = await displayNameProvider.GetAsync(user, HttpContext.RequestAborted),
                    Email = await userManager.GetEmailAsync(user),
                });
        }

        return View(new UserAudienceMembersViewModel
        {
            Name = audience.Name,
            DisplayName = audience.DisplayName,
            IsDefined = audience.IsDefined,
            Count = count,
            List = new AdminListContext<UserAudienceMemberRow>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "name", audience.Name } }),
            },
        });
    }

    private async Task PopulateAsync(UserAudienceEditViewModel model)
    {
        var sources = new List<UserAudienceSourceViewModel>();

        foreach (var source in _sources.OrderBy(source => source.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase))
        {
            sources.Add(new UserAudienceSourceViewModel
            {
                Name = source.Name,
                DisplayName = source.DisplayName,
                NumberLabel = source.NumberLabel?.Value,
                Options = await source.GetOptionsAsync(HttpContext.RequestAborted),
            });
        }

        model.Sources = sources;
    }
}
