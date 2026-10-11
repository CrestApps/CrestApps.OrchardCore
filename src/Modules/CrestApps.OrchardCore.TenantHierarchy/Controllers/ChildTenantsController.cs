using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// The child tenants admin of a parent tenant. It matches the Tenants admin of the Default tenant, but every action
/// takes the identifier of an entry in the parent's own registry and never a tenant name.
/// </summary>
[Admin]
[Feature(TenantHierarchyConstants.Features.Parent)]
public sealed class ChildTenantsController : Controller
{
    private const string FilterSearchKey = "Options.Search";
    private const string FilterStateKey = "Options.State";
    private const string FilterOrderKey = "Options.OrderBy";

    private readonly ChildTenantManager _manager;
    private readonly ITenantHierarchyBroker _broker;
    private readonly DelegatedAccessIssuer _issuer;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ShellSettings _shellSettings;
    private readonly TenantHierarchyOptions _options;
    private readonly IWebHostEnvironment _environment;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantsController"/> class.
    /// </summary>
    /// <param name="manager">The child tenant manager.</param>
    /// <param name="broker">The tenant hierarchy broker.</param>
    /// <param name="issuer">The delegated access issuer.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChildTenantsController(
        ChildTenantManager manager,
        ITenantHierarchyBroker broker,
        DelegatedAccessIssuer issuer,
        HierarchyLabelsProvider labelsProvider,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ShellSettings shellSettings,
        IOptions<TenantHierarchyOptions> options,
        IWebHostEnvironment environment,
        IHtmlLocalizer<ChildTenantsController> htmlLocalizer,
        IStringLocalizer<ChildTenantsController> stringLocalizer)
    {
        _manager = manager;
        _broker = broker;
        _issuer = issuer;
        _labelsProvider = labelsProvider;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shellSettings = shellSettings;
        _options = options.Value;
        _environment = environment;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the child tenants with search, state filter, sort and paging.
    /// </summary>
    /// <param name="options">The search, filter and sort.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("children", "TenantHierarchyChildren")]
    public async Task<IActionResult> Index(
        ChildTenantListOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ViewChildTenants))
        {
            return Forbid();
        }

        options ??= new ChildTenantListOptions();

        var all = await _broker.ListChildrenAsync();
        var filtered = Filter(all, options).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(FilterSearchKey, options.Search);
        }

        if (options.State != ChildTenantStateFilter.All)
        {
            routeData.Values.TryAdd(FilterStateKey, options.State);
        }

        if (options.OrderBy != ChildTenantSort.Name)
        {
            routeData.Values.TryAdd(FilterOrderKey, options.OrderBy);
        }

        // Keeps the page size the user picked when they move to another page.
        if (pagerParameters.PageSize.HasValue)
        {
            routeData.Values.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        var labels = _labelsProvider.GetLabels();
        var enterable = (await _issuer.GetEnterableChildrenAsync(User)).Select(info => info.Entry.EntryId).ToHashSet(StringComparer.Ordinal);

        var model = new ChildTenantsIndexViewModel
        {
            Items = filtered.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, filtered.Count, routeData),
            FilteredCount = filtered.Count,
            TotalCount = all.Count,
            MaxChildren = _manager.GetPolicy().MaxChildren,
            Labels = labels,
            HostGuardInstalled = _manager.IsHostGuardInstalled,
            CanCreate = await AuthorizeAsync(TenantHierarchyPermissions.CreateChildTenants),
            CanManage = await AuthorizeAsync(TenantHierarchyPermissions.ManageChildTenants),
            CanManageFeatures = await AuthorizeAsync(TenantHierarchyPermissions.ManageChildFeatures),
            CanRemove = await AuthorizeAsync(TenantHierarchyPermissions.RemoveChildTenants),
            CanManageAccess = await AuthorizeAsync(TenantHierarchyPermissions.ManageChildAccess),
            EnterableEntryIds = await AuthorizeAsync(TenantHierarchyPermissions.EnterChildTenants) ? enterable : [],
            StateOptions =
            [
                new SelectListItem(S["All states"], nameof(ChildTenantStateFilter.All)),
                new SelectListItem(S["Running"], nameof(ChildTenantStateFilter.Running)),
                new SelectListItem(S["Suspended"], nameof(ChildTenantStateFilter.Suspended)),
                new SelectListItem(S["Setting up"], nameof(ChildTenantStateFilter.SettingUp)),
                new SelectListItem(S["Setup failed"], nameof(ChildTenantStateFilter.Failed)),
                new SelectListItem(S["Pending removal"], nameof(ChildTenantStateFilter.PendingRemoval)),
                new SelectListItem(S["Changed by platform"], nameof(ChildTenantStateFilter.ChangedByPlatform)),
            ],
            SortOptions =
            [
                new SelectListItem(S["Name"], nameof(ChildTenantSort.Name)),
                new SelectListItem(S["Newest first"], nameof(ChildTenantSort.Newest)),
                new SelectListItem(S["State"], nameof(ChildTenantSort.State)),
            ],
        };

        if (model.CanManage)
        {
            model.BulkActions.Add(new SelectListItem(S["Suspend"], nameof(ChildTenantBulkAction.Suspend)));
            model.BulkActions.Add(new SelectListItem(S["Resume"], nameof(ChildTenantBulkAction.Resume)));
            model.BulkActions.Add(new SelectListItem(S["Reload"], nameof(ChildTenantBulkAction.Reload)));
        }

        if (model.CanRemove)
        {
            model.BulkActions.Add(new SelectListItem(S["Remove"], nameof(ChildTenantBulkAction.Remove)));
        }

        return View(model);
    }

    /// <summary>
    /// Applies the search, filter and sort from the list form.
    /// </summary>
    /// <param name="model">The list model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("children", "TenantHierarchyChildren")]
    public IActionResult IndexFilterPost(ChildTenantsIndexViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { FilterSearchKey, model.Options?.Search },
            { FilterStateKey, model.Options?.State },
            { FilterOrderKey, model.Options?.OrderBy },
        });
    }

    /// <summary>
    /// Runs a bulk action on the selected child tenants.
    /// </summary>
    /// <param name="options">The list options with the bulk action.</param>
    /// <param name="itemIds">The registry entries of the selected child tenants.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("children", "TenantHierarchyChildren")]
    public async Task<IActionResult> IndexBulkPost(ChildTenantListOptions options, IEnumerable<string> itemIds)
    {
        var permission = options?.BulkAction == ChildTenantBulkAction.Remove
            ? TenantHierarchyPermissions.RemoveChildTenants
            : TenantHierarchyPermissions.ManageChildTenants;

        if (!IsParent() || !await AuthorizeAsync(permission))
        {
            return Forbid();
        }

        var succeeded = 0;
        var failed = new List<string>();

        foreach (var entryId in (itemIds ?? []).Distinct(StringComparer.Ordinal))
        {
            var result = options.BulkAction switch
            {
                ChildTenantBulkAction.Suspend => await _manager.SuspendAsync(entryId),
                ChildTenantBulkAction.Resume => await _manager.ResumeAsync(entryId),
                ChildTenantBulkAction.Reload => await _manager.ReloadAsync(entryId),
                ChildTenantBulkAction.Remove => await _manager.RemoveAsync(entryId),
                _ => null,
            };

            if (result is null)
            {
                continue;
            }

            if (result.Succeeded)
            {
                succeeded++;
            }
            else
            {
                failed.Add(result.Error);
            }
        }

        if (succeeded > 0)
        {
            await _notifier.SuccessAsync(H["The action was applied to {0} item(s).", succeeded]);
        }

        foreach (var error in failed.Distinct())
        {
            await _notifier.WarningAsync(H["{0}", error]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the screen that creates a child tenant.
    /// </summary>
    [Admin("children/create", "TenantHierarchyChildrenCreate")]
    public async Task<IActionResult> Create()
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.CreateChildTenants))
        {
            return Forbid();
        }

        var model = new ChildTenantEditViewModel();
        await PopulateEditorAsync(model, isNew: true);

        if (model.Recipes.Count == 1)
        {
            model.RecipeName = model.Recipes[0].Value;
        }

        return View(model);
    }

    /// <summary>
    /// Creates a child tenant and starts its setup.
    /// </summary>
    /// <param name="model">The create model.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("children/create", "TenantHierarchyChildrenCreate")]
    public async Task<IActionResult> CreatePost(ChildTenantEditViewModel model)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.CreateChildTenants))
        {
            return Forbid();
        }

        var request = new CreateChildTenantRequest
        {
            DisplayName = model.DisplayName,
            Slug = model.Slug,
            Description = model.Description,
            RecipeName = model.RecipeName,
        };

        foreach (var error in await _manager.ValidateCreateAsync(request))
        {
            ModelState.AddModelError(error.Key, error.Value);
        }

        if (ModelState.IsValid)
        {
            var (result, entry) = await _manager.CreateAsync(request);

            if (result.Succeeded)
            {
                await ChildTenantManager.ScheduleSetup(entry.EntryId);
                await _notifier.SuccessAsync(H["{0} is being set up. It is ready in a moment.", entry.DisplayName]);

                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error);
        }

        await PopulateEditorAsync(model, isNew: true);

        return View(model);
    }

    /// <summary>
    /// Shows the screen that edits a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [Admin("children/{id}/edit", "TenantHierarchyChildrenEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ManageChildTenants))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        var model = new ChildTenantEditViewModel
        {
            EntryId = info.Entry.EntryId,
            DisplayName = info.Entry.DisplayName,
            Slug = info.Entry.Slug,
            Description = info.Entry.Description,
            Info = info,
        };

        await PopulateEditorAsync(model, isNew: false);

        return View(model);
    }

    /// <summary>
    /// Saves the display name, address and description of a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    /// <param name="model">The edit model.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("children/{id}/edit", "TenantHierarchyChildrenEdit")]
    public async Task<IActionResult> EditPost(string id, ChildTenantEditViewModel model)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ManageChildTenants))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        var (result, errors) = await _manager.UpdateAsync(id, model.DisplayName, model.Slug, model.Description);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["{0} was saved.", model.DisplayName]);

            return RedirectToAction(nameof(Index));
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }

        if (errors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.Error);
        }

        model.EntryId = id;
        model.Info = info;
        await PopulateEditorAsync(model, isNew: false);

        return View(model);
    }

    /// <summary>
    /// Returns the address a slug gives and whether it can be used, for the live preview of the create and edit screens.
    /// </summary>
    /// <param name="slug">The slug.</param>
    /// <param name="id">The registry entry being edited, or <see langword="null"/>.</param>
    [HttpGet]
    [Admin("children/check-address", "TenantHierarchyChildrenCheckAddress")]
    public async Task<IActionResult> CheckAddress(string slug, string id = null)
    {
        if (!IsParent() || !(await AuthorizeAsync(TenantHierarchyPermissions.CreateChildTenants) || await AuthorizeAsync(TenantHierarchyPermissions.ManageChildTenants)))
        {
            return Forbid();
        }

        slug = slug?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(id))
        {
            var current = await _broker.GetChildAsync(id);

            if (current is not null && string.Equals(current.Entry.Slug, slug, StringComparison.Ordinal))
            {
                return Json(new { available = true, address = current.Address });
            }
        }

        var errors = await _manager.ValidateCreateAsync(new CreateChildTenantRequest
        {
            DisplayName = "-",
            Slug = slug,
            RecipeName = await GetFirstRecipeNameAsync(),
        });

        var pattern = _manager.GetHostPattern();
        var address = TenantHierarchyNaming.ValidateSlug(slug, _options.ReservedSlugs) == SlugValidationResult.Valid && pattern is not null
            ? $"{GetScheme()}://{TenantHierarchyNaming.BuildHost(pattern, slug)}"
            : null;

        return Json(new
        {
            available = !errors.ContainsKey(nameof(CreateChildTenantRequest.Slug)),
            message = errors.TryGetValue(nameof(CreateChildTenantRequest.Slug), out var message) ? message : null,
            address,
        });
    }

    /// <summary>
    /// Returns the live state of a child tenant, so the list can follow a setup or a removal.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpGet]
    [Admin("children/{id}/status", "TenantHierarchyChildrenStatus")]
    public async Task<IActionResult> Status(string id)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ViewChildTenants))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null)
        {
            return Json(new { status = "Removed" });
        }

        return Json(new
        {
            status = info.Entry.Status.ToString(),
            state = info.State.ToString(),
            canEnter = info.CanEnter,
            error = info.Entry.Error,
        });
    }

    /// <summary>
    /// Suspends a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/suspend", "TenantHierarchyChildrenSuspend")]
    public Task<IActionResult> Suspend(string id)
        => RunAsync(id, TenantHierarchyPermissions.ManageChildTenants, _manager.SuspendAsync, name => H["{0} was suspended.", name]);

    /// <summary>
    /// Resumes a suspended child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/resume", "TenantHierarchyChildrenResume")]
    public Task<IActionResult> Resume(string id)
        => RunAsync(id, TenantHierarchyPermissions.ManageChildTenants, _manager.ResumeAsync, name => H["{0} was resumed.", name]);

    /// <summary>
    /// Reloads a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/reload", "TenantHierarchyChildrenReload")]
    public Task<IActionResult> Reload(string id)
        => RunAsync(id, TenantHierarchyPermissions.ManageChildTenants, _manager.ReloadAsync, name => H["{0} was reloaded.", name]);

    /// <summary>
    /// Restores a child tenant that is pending removal.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/restore", "TenantHierarchyChildrenRestore")]
    public Task<IActionResult> Restore(string id)
        => RunAsync(id, TenantHierarchyPermissions.RemoveChildTenants, _manager.RestoreAsync, name => H["{0} was restored. It stays suspended until you resume it.", name]);

    /// <summary>
    /// Removes a child tenant whose setup failed.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/discard", "TenantHierarchyChildrenDiscard")]
    public Task<IActionResult> Discard(string id)
        => RunAsync(id, TenantHierarchyPermissions.CreateChildTenants, _manager.DiscardAsync, name => H["{0} was discarded.", name]);

    /// <summary>
    /// Removes the registry entry of a child tenant that the platform changed.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/dismiss", "TenantHierarchyChildrenDismiss")]
    public Task<IActionResult> Dismiss(string id)
        => RunAsync(id, TenantHierarchyPermissions.ManageChildTenants, _manager.DismissAsync, name => H["{0} was removed from the list.", name]);

    /// <summary>
    /// Creates a child tenant whose setup failed again.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [HttpPost]
    [Admin("children/{id}/retry", "TenantHierarchyChildrenRetry")]
    public async Task<IActionResult> Retry(string id)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.CreateChildTenants))
        {
            return Forbid();
        }

        var (result, entry) = await _manager.RetryAsync(id);

        if (result.Succeeded)
        {
            await ChildTenantManager.ScheduleSetup(entry.EntryId);
            await _notifier.SuccessAsync(H["{0} is being set up again.", entry.DisplayName]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the screen that confirms the removal of a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [Admin("children/{id}/remove", "TenantHierarchyChildrenRemove")]
    public async Task<IActionResult> Remove(string id)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.RemoveChildTenants))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        return View(new ChildTenantRemoveViewModel
        {
            Info = info,
            GraceDays = _manager.GetPolicy().RemovalGraceDays,
            Labels = _labelsProvider.GetLabels(),
        });
    }

    /// <summary>
    /// Removes a suspended child tenant after the user typed its name.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    /// <param name="model">The confirmation model.</param>
    [HttpPost]
    [ActionName(nameof(Remove))]
    [Admin("children/{id}/remove", "TenantHierarchyChildrenRemove")]
    public async Task<IActionResult> RemovePost(string id, ChildTenantRemoveViewModel model)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.RemoveChildTenants))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        if (!string.Equals(model.ConfirmName?.Trim(), info.Entry.DisplayName, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(ChildTenantRemoveViewModel.ConfirmName), S["Type the name exactly as it is shown."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _manager.RemoveAsync(id);

            if (result.Succeeded)
            {
                var graceDays = _manager.GetPolicy().RemovalGraceDays;

                await _notifier.SuccessAsync(graceDays > 0
                    ? H["{0} will be removed in {1} day(s). You can restore it until then.", info.Entry.DisplayName, graceDays]
                    : H["{0} was removed.", info.Entry.DisplayName]);

                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error);
        }

        model.Info = info;
        model.GraceDays = _manager.GetPolicy().RemovalGraceDays;
        model.Labels = _labelsProvider.GetLabels();

        return View(model);
    }

    /// <summary>
    /// Shows the features of a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    [Admin("children/{id}/features", "TenantHierarchyChildrenFeatures")]
    public async Task<IActionResult> Features(string id)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ManageChildFeatures))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null || info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return NotFound();
        }

        var features = info.State == ChildTenantRuntimeState.Running
            ? await _broker.GetChildFeaturesAsync(id)
            : [];

        return View(new ChildTenantFeaturesViewModel
        {
            Info = info,
            Categories = features.GroupBy(feature => feature.Category).ToList(),
            EnabledCount = features.Count(feature => feature.IsEnabled),
            Labels = _labelsProvider.GetLabels(),
        });
    }

    /// <summary>
    /// Enables or disables features of a child tenant.
    /// </summary>
    /// <param name="id">The registry entry identifier.</param>
    /// <param name="featureIds">The feature identifiers.</param>
    /// <param name="enable"><see langword="true"/> to enable the features, <see langword="false"/> to disable them.</param>
    [HttpPost]
    [ActionName(nameof(Features))]
    [Admin("children/{id}/features", "TenantHierarchyChildrenFeatures")]
    public async Task<IActionResult> FeaturesPost(string id, string[] featureIds, bool enable)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.ManageChildFeatures))
        {
            return Forbid();
        }

        var result = await _manager.SetFeaturesAsync(id, featureIds ?? [], enable);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(enable
                ? H["The features were enabled."]
                : H["The features were disabled."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectToAction(nameof(Features), new { id });
    }

    /// <summary>
    /// Shows the hosted tenant picker: the child tenants the user may enter, with search, favorites and recent ones.
    /// </summary>
    [Admin("children/switch", "TenantHierarchyChildrenSwitch")]
    public async Task<IActionResult> Switch([FromServices] TenantSwitcherPreferenceStore preferenceStore)
    {
        if (!IsParent() || !await AuthorizeAsync(TenantHierarchyPermissions.EnterChildTenants))
        {
            return Forbid();
        }

        Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";

        var enterable = await _issuer.GetEnterableChildrenAsync(User);
        var preference = await preferenceStore.GetAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var model = TenantSwitchViewModel.Create(enterable, preference, _labelsProvider.GetLabels(), embedded: false);

        if (TempData.TryGetValue("TenantHierarchy.SignedOutEverywhere", out var count) && count is int signedOut)
        {
            model.SignedOutCount = signedOut;
        }

        return View(model);
    }

    private async Task<IActionResult> RunAsync(
        string id,
        Permission permission,
        Func<string, Task<TenantHierarchyResult>> operation,
        Func<string, LocalizedHtmlString> successMessage)
    {
        if (!IsParent() || !await AuthorizeAsync(permission))
        {
            return Forbid();
        }

        var info = await _broker.GetChildAsync(id);

        if (info is null)
        {
            return NotFound();
        }

        var result = await operation(id);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(successMessage(info.Entry.DisplayName));
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", result.Error]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateEditorAsync(ChildTenantEditViewModel model, bool isNew)
    {
        model.Labels = _labelsProvider.GetLabels();
        model.HostPattern = _manager.GetHostPattern();
        model.Scheme = GetScheme();
        model.MaxChildren = _manager.GetPolicy().MaxChildren;
        model.TotalCount = (await _broker.ListChildrenAsync()).Count;

        if (!isNew)
        {
            return;
        }

        var recipes = await _manager.GetAllowedRecipesAsync();
        model.Recipes = recipes
            .Select(recipe => new SelectListItem(recipe.DisplayName ?? recipe.Name, recipe.Name, recipe.Name == model.RecipeName))
            .ToList();
        model.RecipeDescriptions = recipes.ToDictionary(recipe => recipe.Name, recipe => recipe.Description ?? string.Empty);
    }

    private static IEnumerable<ChildTenantInfo> Filter(IEnumerable<ChildTenantInfo> items, ChildTenantListOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var search = options.Search.Trim();
            items = items.Where(item =>
                (item.Entry.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Entry.Host?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Entry.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        items = options.State switch
        {
            ChildTenantStateFilter.Running => items.Where(item => item.State == ChildTenantRuntimeState.Running && item.Entry.Status == ChildTenantStatus.Ready),
            ChildTenantStateFilter.Suspended => items.Where(item => item.State == ChildTenantRuntimeState.Suspended && item.Entry.Status == ChildTenantStatus.Ready),
            ChildTenantStateFilter.SettingUp => items.Where(item => item.Entry.Status == ChildTenantStatus.Provisioning || item.State == ChildTenantRuntimeState.Initializing),
            ChildTenantStateFilter.Failed => items.Where(item => item.Entry.Status == ChildTenantStatus.Failed),
            ChildTenantStateFilter.PendingRemoval => items.Where(item => item.Entry.Status is ChildTenantStatus.PendingRemoval or ChildTenantStatus.Removing),
            ChildTenantStateFilter.ChangedByPlatform => items.Where(item => item.State == ChildTenantRuntimeState.ChangedByPlatform),
            _ => items,
        };

        return options.OrderBy switch
        {
            ChildTenantSort.Newest => items.OrderByDescending(item => item.Entry.CreatedUtc),
            ChildTenantSort.State => items.OrderBy(item => item.State).ThenBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => items.OrderBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };
    }

    private string GetScheme()
        => TenantHierarchyUrls.GetScheme(_options, _environment, Request);

    private async Task<string> GetFirstRecipeNameAsync()
    {
        var recipes = await _manager.GetAllowedRecipesAsync();

        return recipes.Count > 0
            ? recipes[0].Name
            : null;
    }

    private async Task<bool> AuthorizeAsync(Permission permission)
        => await _authorizationService.AuthorizeAsync(User, permission);

    private bool IsParent()
        => _shellSettings.IsParentTenant();
}
