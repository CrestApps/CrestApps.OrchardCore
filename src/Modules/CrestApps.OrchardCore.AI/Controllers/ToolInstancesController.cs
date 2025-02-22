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

[Feature(AIConstants.Feature.AITools)]
public sealed class ToolInstancesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIToolInstanceManager _templateManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIToolInstance> _instanceDisplayDriver;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ToolInstancesController(
        IAIToolInstanceManager instanceManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIToolInstance> instanceDisplayManager,
        IServiceProvider serviceProvider,
        INotifier notifier,
        IHtmlLocalizer<ToolInstancesController> htmlLocalizer,
        IStringLocalizer<ToolInstancesController> stringLocalizer)
    {
        _templateManager = instanceManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _instanceDisplayDriver = instanceDisplayManager;
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/tool/instances", "AIToolInstancesIndex")]
    public async Task<IActionResult> Index(
        AIToolInstanceOptions options,
        PagerParameters pagerParameters,
        [FromServices] IEnumerable<IAIToolSource> toolSources,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _templateManager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var model = new ListToolInstancesViewModel
        {
            Instances = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            SourceNames = toolSources.Select(toolSource => toolSource.Name).Order(),
        };

        foreach (var instance in result.Instances)
        {
            model.Instances.Add(new AIToolInstanceEntry
            {
                Instance = instance,
                Shape = await _instanceDisplayDriver.BuildDisplayAsync(instance, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
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
    [Admin("ai/tool/instances", "AIToolInstancesIndex")]
    public ActionResult IndexFilterPOST(ListProfilesViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/tool/instances/create/{source}", "AIToolInstanceCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var toolSource = _serviceProvider.GetKeyedService<IAIToolSource>(source);

        if (toolSource == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a tool-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _templateManager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Invalid template source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ToolInstanceViewModel
        {
            DisplayName = toolSource.Name,
            Editor = await _instanceDisplayDriver.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/tool/instances/create/{source}", "AIToolInstanceCreate")]
    public async Task<ActionResult> CreatePOST(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var toolSource = _serviceProvider.GetKeyedService<IAIToolSource>(source);

        if (toolSource == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a tool-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _templateManager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Invalid template source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ToolInstanceViewModel
        {
            DisplayName = instance.DisplayText,
            Editor = await _instanceDisplayDriver.UpdateEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _templateManager.SaveAsync(instance);

            await _notifier.SuccessAsync(H["A new instance has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/tool/instances/edit/{id}", "AIToolInstanceEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var instance = await _templateManager.FindByIdAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        var model = new ToolInstanceViewModel
        {
            DisplayName = instance.DisplayText,
            Editor = await _instanceDisplayDriver.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/tool/instances/edit/{id}", "AIToolInstanceEdit")]
    public async Task<ActionResult> EditPOST(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var profile = await _templateManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        // Clone the instance to prevent modifying the original instance in the store.
        var mutableInstance = profile.Clone();

        var model = new ProfileViewModel
        {
            DisplayName = mutableInstance.DisplayText,
            Editor = await _instanceDisplayDriver.UpdateEditorAsync(mutableInstance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _templateManager.SaveAsync(mutableInstance);

            await _notifier.SuccessAsync(H["The instance has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/tool/instances/delete/{id}", "AIToolInstanceDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var instance = await _templateManager.FindByIdAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        if (await _templateManager.DeleteAsync(instance))
        {
            await _notifier.SuccessAsync(H["The instance has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/tool/instances", "AIToolInstancesIndex")]

    public async Task<ActionResult> IndexPost(AIProfileOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
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
                        var instance = await _templateManager.FindByIdAsync(id);

                        if (instance == null)
                        {
                            continue;
                        }

                        if (await _templateManager.DeleteAsync(instance))
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
}
