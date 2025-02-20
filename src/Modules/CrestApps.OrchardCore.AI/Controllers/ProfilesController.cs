using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Controllers;

public sealed class ProfilesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIProfile> _profileDisplayManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ProfilesController(
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIProfile> profileDisplayManager,
        IServiceProvider serviceProvider,
        INotifier notifier,
        IHtmlLocalizer<ProfilesController> htmlLocalizer,
        IStringLocalizer<ProfilesController> stringLocalizer)
    {
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _profileDisplayManager = profileDisplayManager;
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/profiles", "AIProfilesIndex")]
    public async Task<IActionResult> Index(
        AIProfileOptions options,
        PagerParameters pagerParameters,
        [FromServices] IEnumerable<IAIProfileSource> profileSources,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _profileManager.PageAsync(pager.Page, pager.PageSize, new AIProfileQueryContext
        {
            Name = options.Search,
            IsListableOnly = true,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var model = new ListProfilesViewModel
        {
            Profiles = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            SourceNames = profileSources.Select(x => x.TechnicalName).Order(),
        };

        foreach (var profile in result.Profiles)
        {
            model.Profiles.Add(new AIProfileEntry
            {
                Profile = profile,
                Shape = await _profileDisplayManager.BuildDisplayAsync(profile, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        model.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(AIProfileAction.Remove)),
        ];

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/profiles", "AIProfilesIndex")]
    public ActionResult IndexFilterPOST(ListProfilesViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/profile/create/{source}", "AIProfilesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIProfileSource>(source);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a profile-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var profile = await _profileManager.NewAsync(source);

        if (profile == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ProfileViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/profile/create/{source}", "AIProfilesCreate")]
    public async Task<ActionResult> CreatePOST(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIProfileSource>(source);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a profile-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var profile = await _profileManager.NewAsync(source);

        if (profile == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ProfileViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _profileDisplayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _profileManager.SaveAsync(profile);

            await _notifier.SuccessAsync(H["Profile has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/profile/edit/{id}", "AIProfilesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var model = new ProfileViewModel
        {
            DisplayName = profile.Name,
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/profile/edit/{id}", "AIProfilesEdit")]
    public async Task<ActionResult> EditPOST(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        // Clone the profile to prevent modifying the original instance in the store.
        var mutableProfile = profile.Clone();

        var model = new ProfileViewModel
        {
            DisplayName = mutableProfile.DisplayText,
            Editor = await _profileDisplayManager.UpdateEditorAsync(mutableProfile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _profileManager.SaveAsync(mutableProfile);

            await _notifier.SuccessAsync(H["Profile has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/profile/delete/{id}", "AIProfilesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var settings = profile.GetSettings<AIProfileSettings>();

        if (!settings.IsRemovable)
        {
            await _notifier.ErrorAsync(H["The profile cannot be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _profileManager.DeleteAsync(profile))
        {
            await _notifier.SuccessAsync(H["Profile has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the profile."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/profiles", "AIProfilesIndex")]

    public async Task<ActionResult> IndexPost(AIProfileOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case AIProfileAction.None:
                    break;
                case AIProfileAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var profile = await _profileManager.FindByIdAsync(id);

                        if (profile == null)
                        {
                            continue;
                        }

                        var settings = profile.GetSettings<AIProfileSettings>();

                        if (!settings.IsRemovable)
                        {
                            continue;
                        }

                        if (await _profileManager.DeleteAsync(profile))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No profiles were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 profile has been removed successfully.", "{0} profiles have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
