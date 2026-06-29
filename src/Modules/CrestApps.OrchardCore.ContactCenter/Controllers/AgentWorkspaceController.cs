using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides the agent workspace where agents sign in to queues and campaigns.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class AgentWorkspaceController : Controller
{
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkspaceController"/> class.
    /// </summary>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AgentWorkspaceController(
        IAgentPresenceManager presenceManager,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IAuthorizationService authorizationService)
    {
        _presenceManager = presenceManager;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _optionsProvider = optionsProvider;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Displays the agent workspace.
    /// </summary>
    /// <returns>The workspace view.</returns>
    [Admin("contact-center/workspace", "ContactCenterWorkspace")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var queues = await _queueManager.ListEnabledAsync();
        var profile = await _agentManager.FindByUserIdAsync(GetUserId());
        var selectedCampaignIds = profile?.CampaignIds ?? [];
        var selectedSkills = profile?.Skills ?? [];

        return View(new AgentWorkspaceViewModel
        {
            Profile = profile,
            AvailableQueues = [.. queues],
            SelectedQueueIds = profile?.QueueIds ?? [],
            CampaignOptions = await _optionsProvider.GetCampaignOptionsAsync(selectedCampaignIds),
            SelectedCampaignIds = selectedCampaignIds,
            SkillOptions = await _optionsProvider.GetSkillOptionsAsync(selectedSkills),
            SelectedSkills = selectedSkills,
        });
    }

    /// <summary>
    /// Signs the agent in to the selected queues and campaigns.
    /// </summary>
    /// <param name="selectedQueueIds">The queues to sign in to.</param>
    /// <param name="selectedCampaignIds">The campaigns to sign in to.</param>
    /// <param name="selectedSkills">The skills to keep on the agent profile.</param>
    /// <returns>A redirect to the workspace.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(
        IEnumerable<string> selectedQueueIds,
        IEnumerable<string> selectedCampaignIds,
        IEnumerable<string> selectedSkills)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var campaigns = ContactCenterFormHelpers.NormalizeList(selectedCampaignIds);
        var profile = await _presenceManager.SignInAsync(GetUserId(), selectedQueueIds ?? [], campaigns);
        profile.Skills = ContactCenterFormHelpers.NormalizeList(selectedSkills);
        await _agentManager.UpdateAsync(profile);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Signs the agent out.
    /// </summary>
    /// <returns>A redirect to the workspace.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutAgent()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        await _presenceManager.SignOutAsync(GetUserId());

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Sets the agent presence state from the soft phone widget.
    /// </summary>
    /// <param name="status">The presence state.</param>
    /// <param name="presenceReason">The optional reason code.</param>
    /// <returns>A redirect to the referring page or the workspace.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPresence(AgentPresenceStatus status, string presenceReason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        await _presenceManager.SetPresenceAsync(GetUserId(), status, presenceReason);

        if (Request.Headers.Referer.Count > 0)
        {
            return Redirect(Request.Headers.Referer.ToString());
        }

        return RedirectToAction(nameof(Index));
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
