using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Models;
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

[Feature(AIConstants.Feature.DataSources)]
public sealed class DataSourcesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAIDataSourceManager _dataSourceManager;
    private readonly AIOptions _aiOptions;
    private readonly IDisplayManager<AIDataSource> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DataSourcesController(
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IAIDataSourceManager dataSourceManager,
        IDisplayManager<AIDataSource> displayManager,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<DataSourcesController> htmlLocalizer,
        IStringLocalizer<DataSourcesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _dataSourceManager = dataSourceManager;
        _displayManager = displayManager;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public async Task<IActionResult> Index(
        ModelOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _dataSourceManager.PageAsync(pager.Page, pager.PageSize, new QueryContext
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

        var viewModel = new ListSourceModelEntryViewModel<AIDataSource, AIDataSourceKey>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _aiOptions.DataSources.Select(x => x.Key)
            .OrderBy(x => x.ProfileSource)
            .ThenBy(x => x.Type),
        };

        foreach (var record in result.Models)
        {
            viewModel.Models.Add(new ModelEntry<AIDataSource>
            {
                Model = record,
                Shape = await _displayManager.BuildDisplayAsync(record, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
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
    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public ActionResult IndexFilterPost(ListModelViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/data-source/create/{source}/{type}", "AIDataSourceCreate")]
    public async Task<ActionResult> Create(string source, string type)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        if (!_aiOptions.DataSources.TryGetValue(new AIDataSourceKey(source, type), out var service))
        {
            await _notifier.ErrorAsync(H["Unable to find a profile-source named '{0}' with the type '{1}'.", source, type]);

            return RedirectToAction(nameof(Index));
        }

        var dataSource = await _dataSourceManager.NewAsync(source, type);

        if (dataSource == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile-source or type."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ModelViewModel
        {
            DisplayName = service.DisplayName,
            Editor = await _displayManager.BuildEditorAsync(dataSource, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/data-source/create/{source}/{type}", "AIDataSourceCreate")]
    public async Task<ActionResult> CreatePost(string source, string type)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        if (!_aiOptions.DataSources.TryGetValue(new AIDataSourceKey(source, type), out var service))
        {
            await _notifier.ErrorAsync(H["Unable to find a provider with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var deployment = await _dataSourceManager.NewAsync(source, type);

        if (deployment == null)
        {
            await _notifier.ErrorAsync(H["Invalid profile-source or type."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new ModelViewModel
        {
            DisplayName = service.DisplayName,
            Editor = await _displayManager.UpdateEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _dataSourceManager.CreateAsync(deployment);

            await _notifier.SuccessAsync(H["Data source has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/data-source/edit/{id}", "AIDataSourceEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = new ModelViewModel
        {
            DisplayName = deployment.DisplayText,
            Editor = await _displayManager.BuildEditorAsync(deployment, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/data-source/edit/{id}", "AIDataSourceEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        // Clone the deployment to prevent modifying the original instance in the store.
        var mutableProfile = deployment.Clone();

        var model = new ModelViewModel
        {
            DisplayName = mutableProfile.DisplayText,
            Editor = await _displayManager.UpdateEditorAsync(mutableProfile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _dataSourceManager.UpdateAsync(mutableProfile);

            await _notifier.SuccessAsync(H["Data source has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/data-source/delete/{id}", "AIDataSourceDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var deployment = await _dataSourceManager.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        await _dataSourceManager.DeleteAsync(deployment);

        await _notifier.SuccessAsync(H["Data source has been deleted successfully."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/data-sources", "AIDataSourcesIndex")]
    public async Task<ActionResult> IndexPost(ModelOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDataSources))
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
                        var dataSource = await _dataSourceManager.FindByIdAsync(id);

                        if (dataSource == null)
                        {
                            continue;
                        }

                        if (await _dataSourceManager.DeleteAsync(dataSource))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No data sources were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 data source has been removed successfully.", "{0} data sources have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
