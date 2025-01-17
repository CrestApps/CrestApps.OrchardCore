using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
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

namespace CrestApps.OrchardCore.OpenAI.Controllers;

public sealed class DeploymentsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IOpenAIDeploymentManager _deploymentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OpenAIDeployment> _deploymentDisplayManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAIConnectionOptions _connectionOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DeploymentsController(
        IOpenAIDeploymentManager deploymentManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OpenAIDeployment> deploymentDisplayManager,
        IServiceProvider serviceProvider,
        IOptions<OpenAIConnectionOptions> connectionOptions,
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

    [Admin("OpenAI/Deployments", "OpenAIDeploymentsIndex")]
    public async Task<IActionResult> Index(
        OpenAIDeploymentOptions options,
        PagerParameters pagerParameters,
        [FromServices] IEnumerable<IOpenAIDeploymentSource> deploymentSources,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
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
            SourceNames = deploymentSources.Select(x => x.TechnicalName).Order(),
        };

        foreach (var deployment in result.Deployments)
        {
            model.Deployments.Add(new OpenAIDeploymentEntry
            {
                Deployment = deployment,
                Shape = await _deploymentDisplayManager.BuildDisplayAsync(deployment, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        model.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(OpenAIDeploymentAction.Remove)),
        ];

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("OpenAI/Deployments", "OpenAIDeploymentsIndex")]
    public ActionResult IndexFilterPOST(ListDeploymentsViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("OpenAI/Deployments/Create/{source}", "OpenAIDeploymentsCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IOpenAIDeploymentSource>(source);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a deployment-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(source);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid deployment source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new OpenAIDeploymentViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("OpenAI/Deployments/Create/{source}", "OpenAIDeploymentsCreate")]
    public async Task<ActionResult> CreatePOST(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var provider = _serviceProvider.GetKeyedService<IOpenAIDeploymentSource>(source);

        if (provider == null)
        {
            await _notifier.ErrorAsync(H["Unable to find a deployment-source that can handle the source '{Source}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _deploymentManager.NewAsync(source);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid deployment source."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new OpenAIDeploymentViewModel
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

    [Admin("OpenAI/Deployments/Edit/{id}", "OpenAIDeploymentsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        var deployment = await _deploymentManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = new ChatProfileViewModel
        {
            DisplayName = deployment.Name,
            Editor = await _deploymentDisplayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("OpenAI/Deployments/Edit/{id}", "OpenAIDeploymentsEdit")]
    public async Task<ActionResult> EditPOST(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
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

        var model = new OpenAIDeploymentViewModel
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
    [Admin("OpenAI/Deployments/Delete/{id}", "OpenAIDeploymentsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
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
    [Admin("OpenAI/Deployments", "OpenAIDeploymentsIndex")]
    public async Task<ActionResult> IndexPost(OpenAIDeploymentOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OpenAIChatPermissions.ManageModelDeployments))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case OpenAIDeploymentAction.None:
                    break;
                case OpenAIDeploymentAction.Remove:
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
