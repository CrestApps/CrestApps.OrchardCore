using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
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

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Manages AI personas in the admin area: the reusable characters AI profiles, profile templates and chat
/// interactions link to. The list shows saved personas and persona files; a persona file is read-only until it is
/// saved, which creates a saved persona that replaces the file.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
public sealed class AIPersonasController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIPersonaManager _personaManager;
    private readonly INamedCatalog<AIPersona> _personasCatalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIPersona> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonasController"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which merges saved personas with persona files.</param>
    /// <param name="personasCatalog">The catalog of saved personas.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager for AI personas.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonasController(
        IAIPersonaManager personaManager,
        INamedCatalog<AIPersona> personasCatalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIPersona> displayManager,
        INotifier notifier,
        IHtmlLocalizer<AIPersonasController> htmlLocalizer,
        IStringLocalizer<AIPersonasController> stringLocalizer)
    {
        _personaManager = personaManager;
        _personasCatalog = personasCatalog;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the saved personas and the persona files.
    /// </summary>
    /// <param name="options">The filter options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("ai/personas", "AIPersonasIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);

        // Persona files are not in the store, so the list is merged and paged in memory.
        var personas = (await _personaManager.GetAllAsync())
            .Where(persona => string.IsNullOrEmpty(options.Search) ||
                (persona.Name?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (persona.DisplayText?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(persona => persona.DisplayText ?? persona.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<AIPersona>>
        {
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, personas.Count, routeData),
        };

        // The admin-list tag builds a row per persona, in the layout (List or Grid) the site or the user picked.
        viewModel.List = new AdminListContext<AIPersona>
        {
            Source = personas.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
            Pager = (IShape)viewModel.Pager,
        };

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    /// <summary>
    /// Handles the filter form of the persona list.
    /// </summary>
    /// <param name="model">The list view model holding the filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/personas", "AIPersonasIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Displays the editor for a new persona.
    /// </summary>
    [Admin("ai/persona/create", "AIPersonasCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var persona = await _personaManager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.BuildEditorAsync(persona, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates a persona from the editor.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/persona/create", "AIPersonasCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var persona = await _personaManager.NewAsync();

        var model = new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.UpdateEditorAsync(persona, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid && await ValidateAsync(persona))
        {
            await _personaManager.CreateAsync(persona);

            await _notifier.SuccessAsync(H["The persona has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Displays the editor of a saved persona or a persona file.
    /// </summary>
    /// <param name="id">The persona's identifier.</param>
    [Admin("ai/persona/edit/{id}", "AIPersonasEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var persona = string.IsNullOrEmpty(id)
            ? null
            : await _personaManager.FindByIdAsync(id);

        if (persona is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = persona.DisplayText ?? persona.Name,
            Editor = await _displayManager.BuildEditorAsync(persona, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a persona from the editor. Saving a persona file creates a saved persona with the same name, which
    /// replaces the file.
    /// </summary>
    /// <param name="id">The persona's identifier.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/persona/edit/{id}", "AIPersonasEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var persona = string.IsNullOrEmpty(id)
            ? null
            : await _personaManager.FindByIdAsync(id);

        if (persona is null)
        {
            return NotFound();
        }

        var saved = await _personasCatalog.FindByIdAsync(id);

        // A persona file is never changed in place: its saved copy keeps the name, so every link follows it.
        var target = saved ?? await _personaManager.NewAsync(persona.Name);

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = persona.DisplayText ?? persona.Name,
            Editor = await _displayManager.UpdateEditorAsync(target, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid && await ValidateAsync(target))
        {
            if (saved is null)
            {
                await _personaManager.CreateAsync(target);
            }
            else
            {
                await _personaManager.UpdateAsync(target);
            }

            await _notifier.SuccessAsync(H["The persona has been saved successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    /// Deletes a saved persona. A persona file cannot be deleted.
    /// </summary>
    /// <param name="id">The persona's identifier.</param>
    [HttpPost]
    [Admin("ai/persona/delete/{id}", "AIPersonasDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
        {
            return Forbid();
        }

        var persona = string.IsNullOrEmpty(id)
            ? null
            : await _personasCatalog.FindByIdAsync(id);

        if (persona is null)
        {
            return NotFound();
        }

        if (await _personaManager.DeleteAsync(persona))
        {
            await _notifier.SuccessAsync(H["The persona has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to delete the persona."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Applies a bulk action to the selected personas. Persona files are skipped.
    /// </summary>
    /// <param name="options">The options holding the bulk action.</param>
    /// <param name="itemIds">The identifiers of the selected personas.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/personas", "AIPersonasIndex")]
    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIPersonas))
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
                        var persona = await _personasCatalog.FindByIdAsync(id);

                        if (persona is not null && await _personaManager.DeleteAsync(persona))
                        {
                            counter++;
                        }
                    }

                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No personas were removed. Persona files cannot be removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 persona has been removed successfully.", "{0} personas have been removed successfully."));
                    }

                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateAsync(AIPersona persona)
    {
        var result = await _personaManager.ValidateAsync(persona);

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.ErrorMessage);
        }

        return result.Succeeded;
    }
}
