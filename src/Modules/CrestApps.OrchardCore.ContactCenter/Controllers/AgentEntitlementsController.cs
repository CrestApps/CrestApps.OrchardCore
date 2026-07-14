using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of manager-owned agent queue and campaign entitlements.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class AgentEntitlementsController : Controller
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly UserManager<IUser> _userManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEntitlementsController"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="optionsProvider">The Contact Center form options provider.</param>
    /// <param name="userManager">The Orchard user manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The admin notifier.</param>
    /// <param name="clock">The clock used to stamp new agent profiles.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentEntitlementsController(
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        ContactCenterAdminFormOptionsProvider optionsProvider,
        UserManager<IUser> userManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AgentEntitlementsController> htmlLocalizer,
        IStringLocalizer<AgentEntitlementsController> stringLocalizer)
    {
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _optionsProvider = optionsProvider;
        _userManager = userManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists configured agent entitlements.
    /// </summary>
    /// <returns>The entitlement list.</returns>
    [Admin("contact-center/agent-entitlements", "ContactCenterAgentEntitlementsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageAgents))
        {
            return Forbid();
        }

        var agents = (await _agentManager.GetAllAsync())
            .OrderBy(agent => agent.UserName ?? agent.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return View(agents);
    }

    /// <summary>
    /// Displays the agent entitlement create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/agent-entitlements/create", "ContactCenterAgentEntitlementsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageAgents))
        {
            return Forbid();
        }

        var model = new AgentEntitlementViewModel
        {
            UserSearchEndpoint = Url.Content("~/Admin/api/crestapps/users/search?valueType=userName"),
        };

        await _optionsProvider.PopulateAgentEntitlementEditorAsync(model);

        return View(model);
    }

    /// <summary>
    /// Creates manager-owned entitlements for an Orchard user.
    /// </summary>
    /// <param name="model">The submitted entitlement model.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [Admin("contact-center/agent-entitlements/create", "ContactCenterAgentEntitlementsCreate")]
    public async Task<IActionResult> Create(AgentEntitlementViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageAgents))
        {
            return Forbid();
        }

        var user = string.IsNullOrWhiteSpace(model.UserName)
            ? null
            : await _userManager.FindByNameAsync(model.UserName.Trim());

        if (user is null)
        {
            ModelState.AddModelError(nameof(model.UserName), S["Select a valid Orchard user."]);
        }

        var userId = user is null ? null : await _userManager.GetUserIdAsync(user);

        if (!string.IsNullOrEmpty(userId) &&
            await _agentManager.FindByUserIdAsync(userId) is not null)
        {
            ModelState.AddModelError(nameof(model.UserName), S["Agent entitlements already exist for this user."]);
        }

        model.AllowedQueueIds = await _optionsProvider.FilterExistingQueueIdsAsync(model.AllowedQueueIds);
        model.AllowedCampaignIds = await _optionsProvider.FilterExistingCampaignIdsAsync(model.AllowedCampaignIds);

        if (ModelState.IsValid)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var agent = await _agentManager.NewAsync();
            agent.UserId = userId;
            agent.UserName = userName;
            agent.DisplayName = userName;
            agent.Name = userId;
            agent.AllowedQueueIds = model.AllowedQueueIds;
            agent.AllowedCampaignIds = model.AllowedCampaignIds;
            agent.CreatedUtc = _clock.UtcNow;

            await _agentManager.CreateAsync(agent);
            await _notifier.SuccessAsync(H["Agent entitlements have been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        model.UserSearchEndpoint = Url.Content("~/Admin/api/crestapps/users/search?valueType=userName");
        await _optionsProvider.PopulateAgentEntitlementEditorAsync(model);

        return View(model);
    }

    /// <summary>
    /// Displays the agent entitlement edit form.
    /// </summary>
    /// <param name="id">The agent profile identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/agent-entitlements/edit/{id}", "ContactCenterAgentEntitlementsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageAgents))
        {
            return Forbid();
        }

        var agent = await _agentManager.FindByIdAsync(id);

        if (agent is null)
        {
            return NotFound();
        }

        var model = CreateViewModel(agent);
        await _optionsProvider.PopulateAgentEntitlementEditorAsync(model);

        return View(model);
    }

    /// <summary>
    /// Updates manager-owned entitlements for an agent.
    /// </summary>
    /// <param name="id">The agent profile identifier.</param>
    /// <param name="model">The submitted entitlement model.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [Admin("contact-center/agent-entitlements/edit/{id}", "ContactCenterAgentEntitlementsEdit")]
    public async Task<IActionResult> Edit(string id, AgentEntitlementViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageAgents))
        {
            return Forbid();
        }

        var agent = await _agentManager.FindByIdAsync(id);

        if (agent is null)
        {
            return NotFound();
        }

        model.Id = agent.ItemId;
        model.UserName = agent.UserName;
        model.AllowedQueueIds = await _optionsProvider.FilterExistingQueueIdsAsync(model.AllowedQueueIds);
        model.AllowedCampaignIds = await _optionsProvider.FilterExistingCampaignIdsAsync(model.AllowedCampaignIds);

        if (ModelState.IsValid)
        {
            await _presenceManager.UpdateEntitlementsAsync(
                id,
                model.AllowedQueueIds,
                model.AllowedCampaignIds);
            await _notifier.SuccessAsync(H["Agent entitlements have been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        await _optionsProvider.PopulateAgentEntitlementEditorAsync(model);

        return View(model);
    }

    private static AgentEntitlementViewModel CreateViewModel(AgentProfile agent)
    {
        return new AgentEntitlementViewModel
        {
            Id = agent.ItemId,
            UserName = agent.UserName,
            AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds(agent.AllowedQueueIds),
            AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds(agent.AllowedCampaignIds),
        };
    }
}
