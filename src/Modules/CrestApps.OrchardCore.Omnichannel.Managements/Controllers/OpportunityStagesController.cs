using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides the administration screens for opportunity stage entries.
/// </summary>
[Admin]
public sealed class OpportunityStagesController : Controller
{
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<OpportunityStage> _manager;
    private readonly INamedCatalog<OpportunityStage> _catalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OpportunityStage> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStagesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="catalog">The named catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityStagesController(
        INamedCatalogManager<OpportunityStage> manager,
        INamedCatalog<OpportunityStage> catalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OpportunityStage> displayManager,
        INotifier notifier,
        IHtmlLocalizer<OpportunityStagesController> htmlLocalizer,
        IStringLocalizer<OpportunityStagesController> stringLocalizer)
    {
        _manager = manager;
        _catalog = catalog;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists every opportunity stage in order.
    /// </summary>
    [Admin("omnichannel/opportunity-stages", "OpportunityStagesIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
        {
            return Forbid();
        }

        var entries = (await _manager.GetAllAsync())
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        var models = new List<CatalogEntryViewModel<OpportunityStage>>();

        foreach (var entry in entries)
        {
            models.Add(new CatalogEntryViewModel<OpportunityStage>
            {
                Model = entry,
                Shape = await _displayManager.BuildDisplayAsync(entry, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(models);
    }

    /// <summary>
    /// Shows the editor for a new opportunity stage.
    /// </summary>
    [Admin("omnichannel/opportunity-stages/create", "OpportunityStagesCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Opportunity Stage"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Creates a new opportunity stage.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/opportunity-stages/create", "OpportunityStagesCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Opportunity Stage"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(OpportunityStage));

        if (isValid && ModelState.IsValid)
        {
            if (await _catalog.FindByNameAsync(model.Name) is not null)
            {
                ModelState.AddModelError(_nameFieldName, S["A opportunity stage with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _notifier.SuccessAsync(H["The opportunity stage has been created successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Shows the editor of a opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    [Admin("omnichannel/opportunity-stages/edit/{id}", "OpportunityStagesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
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
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Updates a opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/opportunity-stages/edit/{id}", "OpportunityStagesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
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
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(OpportunityStage));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _catalog.FindByNameAsync(model.Name);

            if (existing != null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(_nameFieldName, S["A opportunity stage with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _notifier.SuccessAsync(H["The opportunity stage has been updated successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes a opportunity stage.
    /// </summary>
    /// <param name="id">The opportunity stage id.</param>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/opportunity-stages/delete/{id}", "OpportunityStagesDelete")]
    public async Task<ActionResult> DeletePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageOpportunityStages))
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
            await _notifier.SuccessAsync(H["The opportunity stage has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
