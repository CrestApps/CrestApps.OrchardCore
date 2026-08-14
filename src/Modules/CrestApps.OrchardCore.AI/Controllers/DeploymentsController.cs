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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Provides admin controller actions for managing AI deployments.
/// </summary>
[Feature(AIConstants.Feature.Area)]
public sealed class DeploymentsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly INamedSourceCatalogManager<AIDeployment> _deploymentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly AIOptions _aiOptions;
    private readonly IDisplayManager<AIDeployment> _deploymentDisplayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentsController"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment catalog manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="aiOptions">The AI options.</param>
    /// <param name="deploymentDisplayManager">The deployment display manager.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DeploymentsController(
        INamedSourceCatalogManager<AIDeployment> deploymentManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<AIOptions> aiOptions,
        IDisplayManager<AIDeployment> deploymentDisplayManager,
        INotifier notifier,
        IHtmlLocalizer<DeploymentsController> htmlLocalizer,
        IStringLocalizer<DeploymentsController> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _aiOptions = aiOptions.Value;
        _deploymentDisplayManager = deploymentDisplayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of AI deployments.
    /// </summary>
    /// <param name="options">The catalog entry filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The index view.</returns>
    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var allEntries = await _deploymentManager.GetAllAsync();

        IEnumerable<AIDeployment> filtered = allEntries;

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

        var viewModel = new ListCatalogEntryWithReadOnlyViewModel<AIDeployment>
        {
            Models = [],
            ReadOnlyModels = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, editableCount, routeData),
            Sources = _aiOptions.Deployments.Select(x => x.Key).Order(),
        };

        foreach (var record in pagedEditable)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIDeployment>
            {
                Model = record,
                Shape = await _deploymentDisplayManager.BuildDisplayAsync(record, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        foreach (var record in readOnlyEntries)
        {
            viewModel.ReadOnlyModels.Add(new CatalogEntryViewModel<AIDeployment>
            {
                Model = record,
                Shape = await _deploymentDisplayManager.BuildDisplayAsync(record, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form submission for the deployments index.
    /// </summary>
    /// <param name="model">The list view model containing filter options.</param>
    /// <returns>A redirect to the filtered index view.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the editor for creating a new AI deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider.</param>
    /// <returns>The create view.</returns>
    [Admin("ai/deployment/create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> Create(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        if (!_aiOptions.Deployments.TryGetValue(providerName, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{0}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(providerName);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid provider."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for creating a new AI deployment.
    /// </summary>
    /// <param name="providerName">The name of the AI provider.</param>
    /// <returns>A redirect to the index view on success, or the create view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/deployment/create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> CreatePost(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        if (!_aiOptions.Deployments.TryGetValue(providerName, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{0}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(providerName);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid provider."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.UpdateEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _deploymentManager.CreateAsync(deployment);

            await _notifier.SuccessAsync(H["Deployment has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the editor for editing an existing AI deployment.
    /// </summary>
    /// <param name="id">The unique identifier of the deployment.</param>
    /// <returns>The edit view.</returns>
    [Admin("ai/deployment/edit/{id}", "AIDeploymentsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        if (deployment.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This deployment is defined in configuration and cannot be modified."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = deployment.Name,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for updating an existing AI deployment.
    /// </summary>
    /// <param name="id">The unique identifier of the deployment.</param>
    /// <returns>A redirect to the index view on success, or the edit view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/deployment/edit/{id}", "AIDeploymentsEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        if (deployment.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This deployment is defined in configuration and cannot be modified."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = deployment.Name,
            Editor = await _deploymentDisplayManager.UpdateEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _deploymentManager.UpdateAsync(deployment);

            await _notifier.SuccessAsync(H["Deployment has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes an AI deployment by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the deployment to delete.</param>
    /// <returns>A redirect to the index view.</returns>
    [HttpPost]
    [Admin("ai/deployment/delete/{id}", "AIDeploymentsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        if (deployment.IsReadOnly)
        {
            await _notifier.WarningAsync(H["This deployment is defined in configuration and cannot be deleted."]);

            return RedirectToAction(nameof(Index));
        }

        await _deploymentManager.DeleteAsync(deployment);

        await _notifier.SuccessAsync(H["Deployment has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles the bulk action form submission for AI deployments.
    /// </summary>
    /// <param name="options">The catalog entry options containing the selected bulk action.</param>
    /// <param name="itemIds">The identifiers of the selected deployments.</param>
    /// <returns>A redirect to the index view.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
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
                        var deployment = await _deploymentManager.FindByIdAsync(id);

                        if (deployment == null || deployment.IsReadOnly)
                        {
                            continue;
                        }

                        if (await _deploymentManager.DeleteAsync(deployment))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No deployments were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 deployment has been removed successfully.", "{0} deployments have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
