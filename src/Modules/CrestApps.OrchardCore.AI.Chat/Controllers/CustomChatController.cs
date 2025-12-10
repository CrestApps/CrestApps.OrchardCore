using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

[Admin]
public sealed class CustomChatController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ICustomChatInstanceManager _instanceManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AICustomChatInstance> _displayManager;
    private readonly AIOptions _aiOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CustomChatController(
        ICustomChatInstanceManager instanceManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AICustomChatInstance> displayManager,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<CustomChatController> htmlLocalizer,
        IStringLocalizer<CustomChatController> stringLocalizer)
    {
        _instanceManager = instanceManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/custom-chat", "CustomChatIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var instances = (await _instanceManager.GetForCurrentUserAsync()).ToList();

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListSourceCatalogEntryViewModel<AICustomChatInstance>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, instances.Count, routeData),
            Sources = _aiOptions.ProfileSources.Select(x => x.Key).Order(),
        };

        foreach (var instance in instances.Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize))
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AICustomChatInstance>
            {
                Model = instance,
                Shape = await _displayManager.BuildDisplayAsync(instance, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/custom-chat", "CustomChatIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/custom-chat/create/{source}", "CustomChatCreate")]
    public async Task<IActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        if (!_aiOptions.ProfileSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a profile-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _instanceManager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayManager.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/custom-chat/create/{source}", "CustomChatCreate")]
    public async Task<IActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        if (!_aiOptions.ProfileSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a profile-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _instanceManager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayManager.UpdateEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _instanceManager.CreateAsync(instance);

            await _notifier.SuccessAsync(H["Custom chat instance created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/custom-chat/edit/{id}", "CustomChatEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdForCurrentUserAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = instance.DisplayText,
            Editor = await _displayManager.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/custom-chat/edit/{id}", "CustomChatEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdForCurrentUserAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = instance.DisplayText,
            Editor = await _displayManager.UpdateEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _instanceManager.UpdateAsync(instance);

            await _notifier.SuccessAsync(H["Custom chat instance updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/custom-chat/delete/{id}", "CustomChatDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdForCurrentUserAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        if (await _instanceManager.DeleteAsync(instance))
        {
            await _notifier.SuccessAsync(H["Custom chat instance deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the custom chat instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/custom-chat", "CustomChatIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var instance = await _instanceManager.FindByIdForCurrentUserAsync(id);

                        if (instance == null)
                        {
                            continue;
                        }

                        if (await _instanceManager.DeleteAsync(instance))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No instances were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 instance has been removed successfully.", "{0} instances have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("ai/custom-chat/chat/{id}", "CustomChatChat")]
    public async Task<IActionResult> Chat(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AICustomChatPermissions.ManageOwnCustomChatInstances))
        {
            return Forbid();
        }

        var instance = await _instanceManager.FindByIdForCurrentUserAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        return View(instance);
    }
}
