using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// The tenant hierarchy screens of the Default tenant: the whole tree, making and editing parents, and the
/// parent-wide actions.
/// </summary>
[Admin]
[Feature(TenantHierarchyConstants.Features.Platform)]
public sealed class PlatformController : Controller
{
    private readonly TenantHierarchyPlatformService _platformService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ShellSettings _shellSettings;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformController"/> class.
    /// </summary>
    /// <param name="platformService">The platform service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="shellSettings">The settings of the Default tenant.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PlatformController(
        TenantHierarchyPlatformService platformService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ShellSettings shellSettings,
        IHtmlLocalizer<PlatformController> htmlLocalizer,
        IStringLocalizer<PlatformController> stringLocalizer)
    {
        _platformService = platformService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shellSettings = shellSettings;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows every parent with its children, the orphans and the tenants that can become parents.
    /// </summary>
    /// <param name="search">Text to find in the name or address of a parent or of one of its children.</param>
    [Admin("tenant-hierarchy", "TenantHierarchyPlatform")]
    public async Task<IActionResult> Index(string search)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var overview = _platformService.GetOverview();

        return View(new PlatformIndexViewModel
        {
            Overview = overview,
            Parents = overview.Parents.Where(parent => Matches(parent, search)).ToList(),
            Search = search,
            HostGuardInstalled = _platformService.IsHostGuardInstalled,
            PlatformDomain = _platformService.GetPlatformDomain(),
        });
    }

    internal static bool Matches(HierarchyTreeNode parent, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var text = search.Trim();

        static bool Contains(HierarchyTreeNode node, string value)
            => node.DisplayName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true ||
                node.Settings.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                node.Settings.RequestUrlHosts.Any(host => host.Contains(value, StringComparison.OrdinalIgnoreCase));

        return Contains(parent, text) || parent.Children.Any(child => Contains(child, text));
    }

    /// <summary>
    /// Shows the screen that makes a tenant a parent.
    /// </summary>
    /// <param name="tenant">The tenant to preselect.</param>
    [Admin("tenant-hierarchy/make-parent", "TenantHierarchyPlatformMakeParent")]
    public async Task<IActionResult> MakeParent(string tenant, [FromServices] ISetupService setupService)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var model = new ParentPolicyEditViewModel
        {
            TenantName = tenant,
            IsNew = true,
        };

        model.FromPolicy(new ParentTenantPolicy());
        await PopulateAsync(model, setupService);

        if (!string.IsNullOrEmpty(tenant) && model.Suggestions.TryGetValue(tenant, out var suggestion))
        {
            model.DisplayName = suggestion.DisplayName;
            model.Slug = suggestion.Slug;
        }

        return View("EditParent", model);
    }

    /// <summary>
    /// Makes a tenant a parent.
    /// </summary>
    /// <param name="model">The policy model.</param>
    [HttpPost]
    [ActionName(nameof(MakeParent))]
    [Admin("tenant-hierarchy/make-parent", "TenantHierarchyPlatformMakeParent")]
    public async Task<IActionResult> MakeParentPost(ParentPolicyEditViewModel model, [FromServices] ISetupService setupService)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var (result, errors) = await _platformService.MakeParentAsync(model.TenantName, model.Slug, model.DisplayName, model.ToPolicy());

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0} is now a parent tenant.", model.DisplayName]);

            return RedirectToAction(nameof(Parent), new { id = model.TenantName });
        }

        AddErrors(result, errors);
        model.IsNew = true;
        await PopulateAsync(model, setupService);

        return View("EditParent", model);
    }

    /// <summary>
    /// Shows a parent with its children and the problems found.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    [Admin("tenant-hierarchy/parents/{id}", "TenantHierarchyPlatformParent")]
    public async Task<IActionResult> Parent(string id)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var node = await _platformService.GetParentDetailAsync(id);

        if (node is null)
        {
            return NotFound();
        }

        return View(new ParentDetailViewModel
        {
            Parent = node,
            OtherParents = _platformService.GetOverview().Parents
                .Where(parent => parent.Settings.Name != node.Settings.Name)
                .Select(parent => new SelectListItem(parent.DisplayName, parent.Settings.Name))
                .ToList(),
        });
    }

    /// <summary>
    /// Shows the screen that edits the policy of a parent.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    [Admin("tenant-hierarchy/parents/{id}/policy", "TenantHierarchyPlatformPolicy")]
    public async Task<IActionResult> EditParent(string id, [FromServices] ISetupService setupService)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var node = _platformService.GetOverview().Parents.FirstOrDefault(parent => parent.Settings.Name == id);

        if (node is null)
        {
            return NotFound();
        }

        var model = new ParentPolicyEditViewModel
        {
            TenantName = id,
            DisplayName = node.DisplayName,
            Slug = node.Settings.GetHierarchySlug(),
            Host = node.Settings.GetPrimaryHost(),
        };

        model.FromPolicy(node.Settings.GetParentPolicy());
        await PopulateAsync(model, setupService);

        return View(model);
    }

    /// <summary>
    /// Saves the policy of a parent and copies the parts a child enforces into its children.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    /// <param name="model">The policy model.</param>
    [HttpPost]
    [ActionName(nameof(EditParent))]
    [Admin("tenant-hierarchy/parents/{id}/policy", "TenantHierarchyPlatformPolicy")]
    public async Task<IActionResult> EditParentPost(string id, ParentPolicyEditViewModel model, [FromServices] ISetupService setupService)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var (result, errors) = await _platformService.UpdateParentAsync(id, model.DisplayName, model.ToPolicy());

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The policy of {0} was saved.", model.DisplayName]);

            return RedirectToAction(nameof(Parent), new { id });
        }

        AddErrors(result, errors);
        model.TenantName = id;
        await PopulateAsync(model, setupService);

        return View(model);
    }

    /// <summary>
    /// Makes a parent without children an ordinary tenant again.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/parents/{id}/unmake", "TenantHierarchyPlatformUnmake")]
    public Task<IActionResult> Unmake(string id)
        => RunAsync(id, _platformService.UnmakeParentAsync, H["The tenant is no longer a parent tenant."], toIndex: true);

    /// <summary>
    /// Suspends a parent and its running children.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/parents/{id}/suspend", "TenantHierarchyPlatformSuspend")]
    public Task<IActionResult> Suspend(string id)
        => RunAsync(id, _platformService.SuspendParentAsync, H["The parent tenant and its child tenants were suspended."], toIndex: false);

    /// <summary>
    /// Resumes a parent and the children suspended with it.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/parents/{id}/resume", "TenantHierarchyPlatformResume")]
    public Task<IActionResult> Resume(string id)
        => RunAsync(id, _platformService.ResumeParentAsync, H["The parent tenant and the child tenants suspended with it were resumed."], toIndex: false);

    /// <summary>
    /// Removes a suspended parent and all its children after the user typed the parent's tenant name.
    /// </summary>
    /// <param name="id">The tenant name of the parent.</param>
    /// <param name="confirmName">The tenant name the user typed.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/parents/{id}/remove", "TenantHierarchyPlatformRemove")]
    public async Task<IActionResult> Remove(string id, string confirmName)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (!string.Equals(confirmName?.Trim(), id, StringComparison.Ordinal))
        {
            await _notifier.ErrorAsync(H["Type the tenant name exactly as it is shown to remove it."]);

            return RedirectToAction(nameof(Parent), new { id });
        }

        return await RunAsync(id, _platformService.RemoveParentAsync, H["The parent tenant and all its child tenants were removed."], toIndex: true);
    }

    /// <summary>
    /// Moves a child to another parent.
    /// </summary>
    /// <param name="id">The tenant name of the current parent.</param>
    /// <param name="child">The tenant name of the child.</param>
    /// <param name="newParent">The tenant name of the new parent.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/parents/{id}/move", "TenantHierarchyPlatformMove")]
    public async Task<IActionResult> Move(string id, string child, string newParent)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var result = await _platformService.MoveChildAsync(child, newParent);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The child tenant was moved."]);

            return RedirectToAction(nameof(Parent), new { id = newParent });
        }

        await _notifier.ErrorAsync(H["{0}", result.Error]);

        return string.IsNullOrEmpty(id)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Parent), new { id });
    }

    /// <summary>
    /// Makes an orphaned child an ordinary tenant.
    /// </summary>
    /// <param name="child">The tenant name of the child.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/orphans/detach", "TenantHierarchyPlatformDetach")]
    public Task<IActionResult> Detach(string child)
        => RunAsync(child, _platformService.DetachChildAsync, H["The tenant is no longer a child tenant."], toIndex: true);

    /// <summary>
    /// Moves an orphaned child to a parent.
    /// </summary>
    /// <param name="child">The tenant name of the child.</param>
    /// <param name="newParent">The tenant name of the new parent.</param>
    [HttpPost]
    [Admin("tenant-hierarchy/orphans/adopt", "TenantHierarchyPlatformAdopt")]
    public Task<IActionResult> Adopt(string child, string newParent)
        => Move(null, child, newParent);

    private async Task<IActionResult> RunAsync(string id, Func<string, Task<TenantHierarchyResult>> operation, LocalizedHtmlString success, bool toIndex)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        var result = await operation(id);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return toIndex || !result.Succeeded && string.IsNullOrEmpty(id)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Parent), new { id });
    }

    private void AddErrors(TenantHierarchyResult result, IReadOnlyDictionary<string, string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(ToFieldName(error.Key), error.Value);
        }

        if (errors.Count == 0 && !result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error);
        }
    }

    private static string ToFieldName(string policyProperty)
    {
        return policyProperty switch
        {
            nameof(ParentTenantPolicy.SessionValidationInterval) => nameof(ParentPolicyEditViewModel.SessionValidationMinutes),
            nameof(ParentTenantPolicy.SessionIdleTimeout) => nameof(ParentPolicyEditViewModel.SessionIdleMinutes),
            nameof(ParentTenantPolicy.SessionLifetime) => nameof(ParentPolicyEditViewModel.SessionLifetimeHours),
            _ => policyProperty,
        };
    }

    private async Task PopulateAsync(ParentPolicyEditViewModel model, ISetupService setupService)
    {
        model.PlatformDomain = _platformService.GetPlatformDomain();

        var candidates = _platformService.GetOverview().Candidates;
        model.Candidates = candidates
            .Select(settings => new SelectListItem($"{settings.Name} ({settings.State})", settings.Name, settings.Name == model.TenantName))
            .ToList();

        // The form fills the display name and address from the tenant the user picks.
        model.Suggestions = candidates.ToDictionary(
            settings => settings.Name,
            settings => new ParentSuggestion
            {
                DisplayName = string.IsNullOrWhiteSpace(settings[TenantHierarchyConstants.SettingsKeys.Description])
                    ? settings.Name
                    : settings[TenantHierarchyConstants.SettingsKeys.Description],
                Slug = TenantHierarchyNaming.SuggestParentSlug(settings.Name, settings.GetPrimaryHost(), model.PlatformDomain),
            },
            StringComparer.Ordinal);
        model.AvailableRecipes = (await setupService.GetSetupRecipesAsync())
            .OrderBy(recipe => recipe.DisplayName ?? recipe.Name, StringComparer.OrdinalIgnoreCase)
            .Select(recipe => new SelectListItem(recipe.DisplayName ?? recipe.Name, recipe.Name, model.Recipes.Contains(recipe.Name, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        model.DatabasePools = _platformService.GetDatabasePoolNames()
            .Select(pool => new SelectListItem(pool, pool, pool == model.DatabasePool))
            .ToList();
    }

    private async Task<bool> CanManageAsync()
    {
        return _shellSettings.IsDefaultShell() &&
            await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.ManageTenantHierarchy);
    }
}
