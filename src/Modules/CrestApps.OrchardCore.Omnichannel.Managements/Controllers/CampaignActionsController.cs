using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

[Admin]
public sealed class CampaignActionsController : Controller
{
    private readonly ISourceCatalogManager<CampaignAction> _manager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<CampaignAction> _displayDriver;
    private readonly CampaignActionOptions _actionOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CampaignActionsController(
        ISourceCatalogManager<CampaignAction> manager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<CampaignAction> displayDriver,
        IOptions<CampaignActionOptions> actionOptions,
        INotifier notifier,
        IHtmlLocalizer<CampaignActionsController> htmlLocalizer,
        IStringLocalizer<CampaignActionsController> stringLocalizer)
    {
        _manager = manager;
        _campaignManager = campaignManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayDriver = displayDriver;
        _actionOptions = actionOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("omnichannel/campaigns/{campaignId}/actions/create/{source}", "OmnichannelCampaignActionsCreate")]
    public async Task<ActionResult> Create(string campaignId, string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var campaign = await _campaignManager.FindByIdAsync(campaignId);

        if (campaign is null)
        {
            return NotFound();
        }

        if (!_actionOptions.ActionTypes.TryGetValue(source, out var entry))
        {
            await _notifier.ErrorAsync(H["Unable to find an action type with the name '{0}'.", source]);

            return RedirectToAction(nameof(CampaignsController.Edit), "Campaigns", new { id = campaignId });
        }

        var model = await _manager.NewAsync(entry.Type);
        model.CampaignId = campaignId;

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = entry.DisplayName,
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/campaigns/{campaignId}/actions/create/{source}", "OmnichannelCampaignActionsCreate")]
    public async Task<ActionResult> CreatePost(string campaignId, string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var campaign = await _campaignManager.FindByIdAsync(campaignId);

        if (campaign is null)
        {
            return NotFound();
        }

        if (!_actionOptions.ActionTypes.TryGetValue(source, out var entry))
        {
            await _notifier.ErrorAsync(H["Unable to find an action type with the name '{0}'.", source]);

            return RedirectToAction(nameof(CampaignsController.Edit), "Campaigns", new { id = campaignId });
        }

        var model = await _manager.NewAsync(entry.Type);
        model.CampaignId = campaignId;

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new campaign action has been created successfully."]);

            return RedirectToAction(nameof(CampaignsController.Edit), "Campaigns", new { id = campaignId });
        }

        return View(viewModel);
    }

    [Admin("omnichannel/campaign-actions/edit/{id}", "OmnichannelCampaignActionsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        ViewData["CampaignId"] = model.CampaignId;

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/campaign-actions/edit/{id}", "OmnichannelCampaignActionsEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.DisplayText,
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The campaign action has been updated successfully."]);

            return RedirectToAction(nameof(CampaignsController.Edit), "Campaigns", new { id = model.CampaignId });
        }

        ViewData["CampaignId"] = model.CampaignId;

        return View(viewModel);
    }

    [HttpPost]
    [Admin("omnichannel/campaign-actions/delete/{id}", "OmnichannelCampaignActionsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var campaignId = model.CampaignId;

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The campaign action has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the campaign action."]);
        }

        return RedirectToAction(nameof(CampaignsController.Edit), "Campaigns", new { id = campaignId });
    }
}
