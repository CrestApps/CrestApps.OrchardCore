using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
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
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Provides admin controller actions for managing AI provider connections.
/// </summary>
[Feature(AIConstants.Feature.ConnectionManagement)]
public sealed class ProviderConnectionsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly INamedSourceCatalogManager<AIProviderConnection> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IDisplayManager<AIProviderConnection> _displayDriver;
    private readonly AIOptions _aiOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderConnectionsController"/> class.
    /// </summary>
    /// <param name="manager">The provider connection catalog manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="instanceDisplayManager">The provider connection display manager.</param>
    /// <param name="aiOptions">The AI options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProviderConnectionsController(
        INamedSourceCatalogManager<AIProviderConnection> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IShellReleaseManager shellReleaseManager,
        IDisplayManager<AIProviderConnection> instanceDisplayManager,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<ProviderConnectionsController> htmlLocalizer,
        IStringLocalizer<ProviderConnectionsController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _shellReleaseManager = shellReleaseManager;
        _displayDriver = instanceDisplayManager;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of AI provider connections.
    /// </summary>
    /// <param name="options">The catalog entry filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The index view.</returns>
    [Admin("ai/provider/connections", "AIProviderConnectionsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        var allEntries = await _manager.GetAllAsync();

        IEnumerable<AIProviderConnection> filtered = allEntries;

        if (!string.IsNullOrEmpty(options.Search))
        {
            filtered = filtered.Where(e => e.Name.Contains(options.Search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = filtered.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var editableEntries = filtered.Where(e => !e.IsReadOnly);
        var readOnlyEntries = filtered.Where(e => e.IsReadOnly);

        var editableCount = editableEntries.Count();
        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagedEditable = editableEntries
            .Skip((pager.Page - 1) * pager.PageSize)
            .Take(pager.PageSize);

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryWithReadOnlyViewModel<AIProviderConnection>
        {
            Models = [],
            ReadOnlyModels = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, editableCount, routeData),
            Sources = _aiOptions.ConnectionSources.Keys.Order(),
        };

        foreach (var model in pagedEditable)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIProviderConnection>
            {
                Model = model,
                Shape = await _displayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        foreach (var model in readOnlyEntries)
        {
            viewModel.ReadOnlyModels.Add(new CatalogEntryViewModel<AIProviderConnection>
            {
                Model = model,
                Shape = await _displayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form submission for the provider connections index.
    /// </summary>
    /// <param name="model">The list view model containing filter options.</param>
    /// <returns>A redirect to the filtered index view.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/provider/connections", "AIProviderConnectionsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the editor for creating a new AI provider connection.
    /// </summary>
    /// <param name="providerName">The name of the AI provider.</param>
    /// <returns>The create view.</returns>
    [Admin("ai/provider/connection/create/{providerName}", "AIProviderConnectionCreate")]
    public async Task<ActionResult> Create(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        if (!_aiOptions.ConnectionSources.TryGetValue(providerName, out var connectionSource))
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{0}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync(providerName);

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = connectionSource.DisplayName,
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Handles the form submission for creating a new AI provider connection.
    /// </summary>
    /// <param name="providerName">The name of the AI provider.</param>
    /// <returns>A redirect to the index view on success, or the create view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/provider/connection/create/{providerName}", "AIProviderConnectionCreate")]
    public async Task<ActionResult> CreatePost(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        if (!_aiOptions.ConnectionSources.TryGetValue(providerName, out var connectionSource))
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{0}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync(providerName);

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            _shellReleaseManager.RequestRelease();

            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new connection has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the editor for editing an existing AI provider connection.
    /// </summary>
    /// <param name="id">The unique identifier of the connection.</param>
    /// <returns>The edit view.</returns>
    [Admin("ai/provider/connection/edit/{id}", "AIProviderConnectionEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This connection is defined in configuration and cannot be modified."]);

            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Handles the form submission for updating an existing AI provider connection.
    /// </summary>
    /// <param name="id">The unique identifier of the connection.</param>
    /// <returns>A redirect to the index view on success, or the edit view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/provider/connection/edit/{id}", "AIProviderConnectionEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This connection is defined in configuration and cannot be modified."]);

            return RedirectToAction(nameof(Index));
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            _shellReleaseManager.RequestRelease();

            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The connection has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes an AI provider connection by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to delete.</param>
    /// <returns>A redirect to the index view.</returns>
    [HttpPost]
    [Admin("ai/provider/connection/delete/{id}", "AIProviderConnectionDelete")]

    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This connection is defined in configuration and cannot be deleted."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _manager.DeleteAsync(model))
        {
            _shellReleaseManager.RequestRelease();

            await _notifier.SuccessAsync(H["The connection has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the connection."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles the bulk action form submission for AI provider connections.
    /// </summary>
    /// <param name="options">The catalog entry options containing the selected bulk action.</param>
    /// <param name="itemIds">The identifiers of the selected connections.</param>
    /// <returns>A redirect to the index view.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/provider/connections", "AIProviderConnectionsIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageProviderConnections))
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
                        var instance = await _manager.FindByIdAsync(id);

                        if (instance == null || instance.IsReadOnly)
                        {
                            continue;
                        }

                        if (await _manager.DeleteAsync(instance))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No connections were removed."]);
                    }
                    else
                    {
                        _shellReleaseManager.RequestRelease();

                        await _notifier.SuccessAsync(H.Plural(counter, "1 connection has been removed successfully.", "{0} connections have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
