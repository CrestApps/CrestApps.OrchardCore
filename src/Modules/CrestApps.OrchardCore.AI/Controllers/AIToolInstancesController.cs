using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Controller for managing AI tool instances in the admin area. Each instance is a user-configured tool
/// created from a registered tool instance source and surfaced to the AI model as its own function.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
public sealed class AIToolInstancesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ISourceCatalogManager<AIToolInstance> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIToolInstance> _displayManager;
    private readonly AIOptions _aiOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstancesController"/> class.
    /// </summary>
    /// <param name="manager">The tool instance catalog manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager for AI tool instances.</param>
    /// <param name="aiOptions">The AI options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolInstancesController(
        ISourceCatalogManager<AIToolInstance> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIToolInstance> displayManager,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<AIToolInstancesController> htmlLocalizer,
        IStringLocalizer<AIToolInstancesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of AI tool instances.
    /// </summary>
    /// <param name="options">The catalog entry filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The index view with the list of tool instances.</returns>
    [Admin("ai/tool-instances", "AIToolInstancesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Sorted = true,
            Name = options.Search,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListSourceCatalogEntryViewModel<AIToolInstance>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _aiOptions.ToolInstanceSources.Select(x => x.Key).Order(),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIToolInstance>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form submission for the tool instances index page.
    /// </summary>
    /// <param name="model">The list view model containing filter options.</param>
    /// <returns>A redirect to the index action with the applied filter.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/tool-instances", "AIToolInstancesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the form for creating a new AI tool instance.
    /// </summary>
    /// <param name="source">The tool instance source identifier.</param>
    /// <returns>The create view with the editor form.</returns>
    [Admin("ai/tool-instance/create/{source}", "AIToolInstancesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        if (!_aiOptions.ToolInstanceSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a tool instance source that can handle the source '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _manager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new tool instance."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayManager.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for creating a new AI tool instance.
    /// </summary>
    /// <param name="source">The tool instance source identifier.</param>
    /// <returns>A redirect to the index on success, or the create view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/tool-instance/create/{source}", "AIToolInstancesCreate")]
    public async Task<ActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return Forbid();
        }

        if (!_aiOptions.ToolInstanceSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a tool instance source that can handle the source '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var instance = await _manager.NewAsync(source);

        if (instance == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new tool instance."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayManager.UpdateEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(instance);

            await _notifier.SuccessAsync(H["Tool instance has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the form for editing an existing AI tool instance.
    /// </summary>
    /// <param name="id">The unique identifier of the tool instance to edit.</param>
    /// <returns>The edit view with the editor form.</returns>
    [Admin("ai/tool-instance/edit/{id}", "AIToolInstancesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        var instance = await _manager.FindByIdAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        if (!await AuthorizeInstanceAsync(instance))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = instance.Name,
            Editor = await _displayManager.BuildEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for editing an existing AI tool instance.
    /// </summary>
    /// <param name="id">The unique identifier of the tool instance to update.</param>
    /// <returns>A redirect to the index on success, or the edit view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/tool-instance/edit/{id}", "AIToolInstancesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        var instance = await _manager.FindByIdAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        if (!await AuthorizeInstanceAsync(instance))
        {
            return Forbid();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = instance.Name,
            Editor = await _displayManager.UpdateEditorAsync(instance, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(instance);

            await _notifier.SuccessAsync(H["Tool instance has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes an AI tool instance by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the tool instance to delete.</param>
    /// <returns>A redirect to the index action.</returns>
    [HttpPost]
    [Admin("ai/tool-instance/delete/{id}", "AIToolInstancesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var instance = await _manager.FindByIdAsync(id);

        if (instance == null)
        {
            return NotFound();
        }

        if (!await AuthorizeInstanceAsync(instance))
        {
            return Forbid();
        }

        if (await _manager.DeleteAsync(instance))
        {
            await _notifier.SuccessAsync(H["Tool instance has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the tool instance."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles bulk actions on selected AI tool instances.
    /// </summary>
    /// <param name="options">The catalog entry options containing the bulk action to perform.</param>
    /// <param name="itemIds">The identifiers of the selected tool instances.</param>
    /// <returns>A redirect to the index action.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/tool-instances", "AIToolInstancesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
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
                    var skipped = 0;

                    foreach (var id in itemIds)
                    {
                        var instance = await _manager.FindByIdAsync(id);

                        if (instance == null)
                        {
                            continue;
                        }

                        if (!await AuthorizeInstanceAsync(instance))
                        {
                            skipped++;

                            continue;
                        }

                        if (await _manager.DeleteAsync(instance))
                        {
                            counter++;
                        }
                    }

                    if (skipped > 0)
                    {
                        await _notifier.WarningAsync(H.Plural(skipped, "1 tool instance was skipped because it was created by another user.", "{0} tool instances were skipped because they were created by other users."));
                    }

                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No tool instances were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 tool instance has been removed successfully.", "{0} tool instances have been removed successfully."));
                    }

                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Authorizes the current user against the supplied instance. Users may always manage the instances
    /// they own, but managing an instance created by someone else requires the dedicated permission.
    /// </summary>
    private async Task<bool> AuthorizeInstanceAsync(AIToolInstance instance)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstances))
        {
            return false;
        }

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(instance.OwnerId) &&
            !string.Equals(instance.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIToolInstancesCreatedByOthers);
        }

        return true;
    }
}
