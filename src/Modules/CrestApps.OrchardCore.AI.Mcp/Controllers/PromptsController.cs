using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
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
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Mcp.Controllers;

[Feature(McpConstants.Feature.Server)]
public sealed class PromptsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly INamedCatalogManager<McpPrompt> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<McpPrompt> _displayDriver;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PromptsController(
        INamedCatalogManager<McpPrompt> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<McpPrompt> displayDriver,
        INotifier notifier,
        IHtmlLocalizer<PromptsController> htmlLocalizer,
        IStringLocalizer<PromptsController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayDriver = displayDriver;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/mcp/prompts", "AIMCPPromptsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<McpPrompt>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<McpPrompt>
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

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/mcp/prompts", "AIMCPPromptsIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/mcp/prompt/create", "AIMCPPromptCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Prompt"],
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/mcp/prompt/create", "AIMCPPromptCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new prompt has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [Admin("ai/mcp/prompt/edit/{id}", "AIMCPPromptEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/mcp/prompt/edit/{id}", "AIMCPPromptEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The prompt has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [HttpPost]
    [Admin("ai/mcp/prompt/delete/{id}", "AIMCPPromptDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The prompt has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the prompt."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/mcp/prompts", "AIMCPPromptsIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, McpPermissions.ManageMcpPrompts))
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

                        if (instance == null)
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
                        await _notifier.WarningAsync(H["No prompts were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 prompt has been removed successfully.", "{0} prompts have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
