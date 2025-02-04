using CrestApps.OrchardCore.AI.Azure.Core;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Controllers;

[Feature(AIConstants.Feature.Chat)]
public sealed class ChatProfilesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIChatProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIChatProfile> _profileDisplayManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ChatProfilesController(
        IAIChatProfileManager profileManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIChatProfile> profileDisplayManager,
        IServiceProvider serviceProvider,
        INotifier notifier,
        IHtmlLocalizer<ChatProfilesController> htmlLocalizer,
        IStringLocalizer<ChatProfilesController> stringLocalizer)
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

    [Admin("AI/ChatProfiles", "AIChatProfilesIndex")]
    public async Task<IActionResult> Index(
        AIChatProfileOptions options,
        PagerParameters pagerParameters,
        [FromServices] IEnumerable<IAIChatProfileSource> profileSources,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _profileManager.PageAsync(pager.Page, pager.PageSize, new AIChatProfileQueryContext
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

        var model = new ListChatProfilesViewModel
        {
            Profiles = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            SourceNames = profileSources.Select(x => x.TechnicalName).Order(),
        };

        foreach (var profile in result.Profiles)
        {
            model.Profiles.Add(new AIChatProfileEntry
            {
                Profile = profile,
                Shape = await _profileDisplayManager.BuildDisplayAsync(profile, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        model.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(AIChatProfileAction.Remove)),
        ];

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("AI/ChatProfiles", "AIChatProfilesIndex")]
    public ActionResult IndexFilterPOST(ListChatProfilesViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("AI/ChatProfiles/Create/{source}", "AIChatProfilesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIChatProfileSource>(source);

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

        var model = new ChatProfileViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("AI/ChatProfiles/Create/{source}", "AIChatProfilesCreate")]
    public async Task<ActionResult> CreatePOST(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIChatProfileSource>(source);

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

        var model = new ChatProfileViewModel
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

    [Admin("AI/ChatProfiles/Edit/{id}", "AIChatProfilesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var model = new ChatProfileViewModel
        {
            DisplayName = profile.Name,
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("AI/ChatProfiles/Edit/{id}", "AIChatProfilesEdit")]
    public async Task<ActionResult> EditPOST(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
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

        var model = new ChatProfileViewModel
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
    [Admin("AI/ChatProfiles/Delete/{id}", "AIChatProfilesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
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

        var settings = profile.GetSettings<AIChatProfileSettings>();

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
    [Admin("AI/ChatProfiles", "AIChatProfilesIndex")]

    public async Task<ActionResult> IndexPost(AIChatProfileOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageAIChatProfiles))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case AIChatProfileAction.None:
                    break;
                case AIChatProfileAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var profile = await _profileManager.FindByIdAsync(id);

                        if (profile == null)
                        {
                            continue;
                        }

                        var settings = profile.GetSettings<AIChatProfileSettings>();

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
