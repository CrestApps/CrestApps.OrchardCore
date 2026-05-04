using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
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
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Controller for managing AI profile templates in the admin area.
/// </summary>
public sealed class AITemplatesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIProfileTemplateManager _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIProfileTemplate> _displayDriver;
    private readonly AIOptions _aiOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AITemplatesController"/> class.
    /// </summary>
    /// <param name="manager">The AI profile template manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayDriver">The display manager for AI profile templates.</param>
    /// <param name="aiOptions">The AI options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AITemplatesController(
        IAIProfileTemplateManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIProfileTemplate> displayDriver,
        IOptions<AIOptions> aiOptions,
        INotifier notifier,
        IHtmlLocalizer<AITemplatesController> htmlLocalizer,
        IStringLocalizer<AITemplatesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayDriver = displayDriver;
        _aiOptions = aiOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays a paginated list of AI profile templates.
    /// </summary>
    /// <param name="options">The catalog entry filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The index view with the list of templates.</returns>
    [Admin("ai/templates", "AITemplatesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
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

        var viewModel = new ListSourceCatalogEntryViewModel<AIProfileTemplate>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _aiOptions.TemplateSources.Select(x => x.Key).Order(),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIProfileTemplate>
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
    /// Handles the filter form submission for the templates index page.
    /// </summary>
    /// <param name="model">The list view model containing filter options.</param>
    /// <returns>A redirect to the index action with the applied filter.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/templates", "AITemplatesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the form for creating a new AI profile template.
    /// </summary>
    /// <param name="source">The template source identifier.</param>
    /// <returns>The create view with the editor form.</returns>
    [Admin("ai/template/create/{source}", "AITemplatesCreate")]
    public async Task<ActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        if (!_aiOptions.TemplateSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a template-source that can handle the source '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var template = await _manager.NewAsync(source);

        if (template == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new template."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayDriver.BuildEditorAsync(template, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for creating a new AI profile template.
    /// </summary>
    /// <param name="source">The template source identifier.</param>
    /// <returns>A redirect to the index on success, or the create view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/template/create/{source}", "AITemplatesCreate")]
    public async Task<ActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        if (!_aiOptions.TemplateSources.TryGetValue(source, out var provider))
        {
            await _notifier.ErrorAsync(H["Unable to find a template-source that can handle the source '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var template = await _manager.NewAsync(source);

        if (template == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new template."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = provider.DisplayName,
            Editor = await _displayDriver.UpdateEditorAsync(template, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(template);

            await _notifier.SuccessAsync(H["Template has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the form for editing an existing AI profile template.
    /// </summary>
    /// <param name="id">The unique identifier of the template to edit.</param>
    /// <returns>The edit view with the editor form.</returns>
    [Admin("ai/template/edit/{id}", "AITemplatesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        var template = await _manager.FindByIdAsync(id);

        if (template == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = template.DisplayText,
            Editor = await _displayDriver.BuildEditorAsync(template, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    /// <summary>
    /// Handles the form submission for editing an existing AI profile template.
    /// </summary>
    /// <param name="id">The unique identifier of the template to update.</param>
    /// <returns>A redirect to the index on success, or the edit view with validation errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/template/edit/{id}", "AITemplatesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        var template = await _manager.FindByIdAsync(id);

        if (template == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = template.DisplayText,
            Editor = await _displayDriver.UpdateEditorAsync(template, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(template);

            await _notifier.SuccessAsync(H["Template has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes an AI profile template by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the template to delete.</param>
    /// <returns>A redirect to the index action.</returns>
    [HttpPost]
    [Admin("ai/template/delete/{id}", "AITemplatesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
        {
            return Forbid();
        }

        var template = await _manager.FindByIdAsync(id);

        if (template == null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(template))
        {
            await _notifier.SuccessAsync(H["Template has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the template."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles bulk actions on selected AI profile templates.
    /// </summary>
    /// <param name="options">The catalog entry options containing the bulk action to perform.</param>
    /// <param name="itemIds">The identifiers of the selected templates.</param>
    /// <returns>A redirect to the index action.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/templates", "AITemplatesIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfileTemplates))
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
                        await _notifier.WarningAsync(H["No templates were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 template has been removed successfully.", "{0} templates have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
