using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides the agent workspace where agents sign in to queues and campaigns and change presence.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AgentWorkspaceController : Controller
{
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkspaceController"/> class.
    /// </summary>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public AgentWorkspaceController(
        IAgentPresenceManager presenceManager,
        IActivityQueueManager queueManager,
        IAuthorizationService authorizationService)
    {
        _presenceManager = presenceManager;
        _queueManager = queueManager;
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

        return View(new AgentWorkspaceViewModel
        {
            AvailableQueues = [.. queues],
        });
    }

    /// <summary>
    /// Signs the agent in to the selected queues and campaigns.
    /// </summary>
    /// <param name="selectedQueueIds">The queues to sign in to.</param>
    /// <param name="campaignIds">The comma-separated campaigns to sign in to.</param>
    /// <returns>A redirect to the workspace.</returns>
    [HttpPost]
    public async Task<IActionResult> SignIn(IEnumerable<string> selectedQueueIds, string campaignIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var campaigns = string.IsNullOrEmpty(campaignIds)
            ? []
            : campaignIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await _presenceManager.SignInAsync(GetUserId(), selectedQueueIds ?? [], campaigns);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Signs the agent out.
    /// </summary>
    /// <returns>A redirect to the workspace.</returns>
    [HttpPost]
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
    /// Sets the agent presence state.
    /// </summary>
    /// <param name="status">The presence state.</param>
    /// <param name="reason">The optional reason code.</param>
    /// <returns>A redirect to the workspace.</returns>
    [HttpPost]
    public async Task<IActionResult> SetPresence(AgentPresenceStatus status, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        await _presenceManager.SetPresenceAsync(GetUserId(), status, reason);

        return RedirectToAction(nameof(Index));
    }

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
