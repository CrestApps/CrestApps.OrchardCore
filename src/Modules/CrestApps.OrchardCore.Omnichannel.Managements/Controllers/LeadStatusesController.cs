using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
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
/// Provides the administration screens for lead status entries.
/// </summary>
[Admin]
public sealed class LeadStatusesController : Controller
{
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<LeadStatus> _manager;
    private readonly INamedCatalog<LeadStatus> _catalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<LeadStatus> _displayManager;
    private readonly INotifier _notifier;
    private readonly LeadStatusFlagService _flagService;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusesController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="catalog">The named catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="flagService">Keeps the single-status flags on one status.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadStatusesController(
        INamedCatalogManager<LeadStatus> manager,
        INamedCatalog<LeadStatus> catalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<LeadStatus> displayManager,
        INotifier notifier,
        LeadStatusFlagService flagService,
        IHtmlLocalizer<LeadStatusesController> htmlLocalizer,
        IStringLocalizer<LeadStatusesController> stringLocalizer)
    {
        _manager = manager;
        _catalog = catalog;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _flagService = flagService;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists every lead status in order.
    /// </summary>
    [Admin("omnichannel/lead-statuses", "LeadStatusesIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
        {
            return Forbid();
        }

        var entries = (await _manager.GetAllAsync())
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        var models = new List<CatalogEntryViewModel<LeadStatus>>();

        foreach (var entry in entries)
        {
            models.Add(new CatalogEntryViewModel<LeadStatus>
            {
                Model = entry,
                Shape = await _displayManager.BuildDisplayAsync(entry, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(models);
    }

    /// <summary>
    /// Shows the editor for a new lead status.
    /// </summary>
    [Admin("omnichannel/lead-statuses/create", "LeadStatusesCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Lead Status"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Creates a new lead status.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/lead-statuses/create", "LeadStatusesCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Lead Status"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(LeadStatus));

        if (isValid && ModelState.IsValid)
        {
            if (await _catalog.FindByNameAsync(model.Name) is not null)
            {
                ModelState.AddModelError(_nameFieldName, S["A lead status with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _flagService.EnforceAsync(model);
                await _notifier.SuccessAsync(H["The lead status has been created successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Shows the editor of a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    [Admin("omnichannel/lead-statuses/edit/{id}", "LeadStatusesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
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
    /// Updates a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/lead-statuses/edit/{id}", "LeadStatusesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(LeadStatus));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _catalog.FindByNameAsync(model.Name);

            if (existing != null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(_nameFieldName, S["A lead status with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _flagService.EnforceAsync(model);
                await _notifier.SuccessAsync(H["The lead status has been updated successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes a lead status.
    /// </summary>
    /// <param name="id">The lead status id.</param>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("omnichannel/lead-statuses/delete/{id}", "LeadStatusesDelete")]
    public async Task<ActionResult> DeletePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageLeadStatuses))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (model.IsConverted)
        {
            await _notifier.ErrorAsync(H["The converted status cannot be deleted, because converting a lead needs it. Mark another status as the converted status first."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The lead status has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
