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

public sealed class DeploymentsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIDeployment> _deploymentDisplayManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderOptions _connectionOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DeploymentsController(
        IAIDeploymentManager deploymentManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIDeployment> deploymentDisplayManager,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> connectionOptions,
        INotifier notifier,
        IHtmlLocalizer<DeploymentsController> htmlLocalizer,
        IStringLocalizer<DeploymentsController> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _deploymentDisplayManager = deploymentDisplayManager;
        _serviceProvider = serviceProvider;
        _connectionOptions = connectionOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("AI/Deployments", "AIDeploymentsIndex")]
    public async Task<IActionResult> Index(
        AIDeploymentOptions options,
        PagerParameters pagerParameters,
        [FromServices] IEnumerable<IAIDeploymentProvider> deploymentSources,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _deploymentManager.PageQueriesAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var model = new ListDeploymentsViewModel
        {
            Deployments = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            ProviderNames = deploymentSources.Select(x => x.TechnicalName).Order(),
        };

        foreach (var deployment in result.Deployments)
        {
            model.Deployments.Add(new AIDeploymentEntry
            {
                Deployment = deployment,
                Shape = await _deploymentDisplayManager.BuildDisplayAsync(deployment, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        model.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(AIDeploymentAction.Remove)),
        ];

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("AI/Deployments", "AIDeploymentsIndex")]
    public ActionResult IndexFilterPOST(ListDeploymentsViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("AI/Deployments/Create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> Create(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIDeploymentProvider>(providerName);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{ProviderName}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(providerName);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid provider."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new AIDeploymentViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("AI/Deployments/Create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> CreatePOST(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IAIDeploymentProvider>(providerName);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a deployment-source that can handle the source '{ProviderName}'.", providerName]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(providerName);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid provider."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new AIDeploymentViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.UpdateEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _deploymentManager.SaveAsync(deployment);

            await _notifier.SuccessAsync(H["Deployment has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("AI/Deployments/Edit/{id}", "AIDeploymentsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = new AIDeploymentViewModel
        {
            DisplayName = deployment.Name,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("AI/Deployments/Edit/{id}", "AIDeploymentsEdit")]
    public async Task<ActionResult> EditPOST(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        // Clone the deployment to prevent modifying the original instance in the store.
        var mutableProfile = deployment.Clone();

        var model = new AIDeploymentViewModel
        {
            DisplayName = mutableProfile.Name,
            Editor = await _deploymentDisplayManager.UpdateEditorAsync(mutableProfile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _deploymentManager.SaveAsync(mutableProfile);

            await _notifier.SuccessAsync(H["Deployment has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("AI/Deployments/Delete/{id}", "AIDeploymentsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        await _deploymentManager.DeleteAsync(deployment);

        await _notifier.SuccessAsync(H["Deployment has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("AI/Deployments", "AIDeploymentsIndex")]
    public async Task<ActionResult> IndexPost(AIDeploymentOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case AIDeploymentAction.None:
                    break;
                case AIDeploymentAction.Remove:
                    var counter = 0;
                    foreach (var id in itemIds)
                    {
                        var deployment = await _deploymentManager.FindByIdAsync(id);

                        if (deployment == null)
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
