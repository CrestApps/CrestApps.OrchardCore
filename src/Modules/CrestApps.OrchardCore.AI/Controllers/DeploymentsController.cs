using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
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

namespace CrestApps.OrchardCore.AI.Controllers;

public sealed class DeploymentsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly INamedModelManager<AIDeployment> _deploymentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly AICompletionOptions _completionOptions;
    private readonly IDisplayManager<AIDeployment> _deploymentDisplayManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderOptions _connectionOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DeploymentsController(
        INamedModelManager<AIDeployment> deploymentManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IOptions<AICompletionOptions> completionOptions,
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
        _completionOptions = completionOptions.Value;
        _deploymentDisplayManager = deploymentDisplayManager;
        _serviceProvider = serviceProvider;
        _connectionOptions = connectionOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public async Task<IActionResult> Index(
        ModelOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _deploymentManager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Sorted = true,
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListModelViewModel<AIDeployment>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            SourceNames = _completionOptions.Deployments.Select(x => x.Key).Order(),
        };

        foreach (var record in result.Models)
        {
            viewModel.Models.Add(new ModelEntry<AIDeployment>
            {
                Model = record,
                Shape = await _deploymentDisplayManager.BuildDisplayAsync(record, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(ModelAction.Remove)),
        ];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public ActionResult IndexFilterPOST(ListModelViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/deployment/create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> Create(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        if (!_completionOptions.Deployments.TryGetValue(providerName, out var provider))
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

        var model = new ModelViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/deployment/create/{providerName}", "AIDeploymentsCreate")]
    public async Task<ActionResult> CreatePOST(string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        if (!_completionOptions.Deployments.TryGetValue(providerName, out var provider))
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

        var model = new ModelViewModel
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

        var model = new ModelViewModel
        {
            DisplayName = deployment.Name,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/deployment/edit/{id}", "AIDeploymentsEdit")]
    public async Task<ActionResult> EditPOST(string id)
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

        // Clone the deployment to prevent modifying the original instance in the store.
        var mutableProfile = deployment.Clone();

        var model = new ModelViewModel
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

        await _deploymentManager.DeleteAsync(deployment);

        await _notifier.SuccessAsync(H["Deployment has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/deployments", "AIDeploymentsIndex")]
    public async Task<ActionResult> IndexPost(ModelOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case ModelAction.None:
                    break;
                case ModelAction.Remove:
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
