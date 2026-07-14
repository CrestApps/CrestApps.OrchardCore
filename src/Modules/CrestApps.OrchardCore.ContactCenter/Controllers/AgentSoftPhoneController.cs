using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides Contact Center agent work actions used by the soft phone widget.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class AgentSoftPhoneController : Controller
{
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSoftPhoneController"/> class.
    /// </summary>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="logger">The logger.</param>
    public AgentSoftPhoneController(
        IAgentPresenceManager presenceManager,
        IAuthorizationService authorizationService,
        ILogger<AgentSoftPhoneController> logger)
    {
        _presenceManager = presenceManager;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Signs the agent in to the selected queues and campaigns.
    /// </summary>
    /// <param name="selectedQueueIds">The queues to sign in to.</param>
    /// <param name="selectedCampaignIds">The campaigns to sign in to.</param>
    /// <param name="returnUrl">The local URL to return to after sign-in.</param>
    /// <returns>A redirect to the current page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(
        IEnumerable<string> selectedQueueIds,
        IEnumerable<string> selectedCampaignIds,
        string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var queues = ContactCenterFormHelpers.NormalizeList(selectedQueueIds);
        var campaigns = ContactCenterFormHelpers.NormalizeList(selectedCampaignIds);

        if (queues.Count == 0 && campaigns.Count == 0)
        {
            return BadRequest("Select at least one queue or campaign before signing in.");
        }

        try
        {
            await _presenceManager.SignInAsync(GetUserId(), queues, campaigns);
        }
        catch (AgentEntitlementDeniedException exception)
        {
            return BadRequest(exception.Message);
        }

        return RedirectToReturnLocation(returnUrl);
    }

    /// <summary>
    /// Signs the agent out.
    /// </summary>
    /// <param name="returnUrl">The local URL to return to after sign-out.</param>
    /// <returns>A redirect to the current page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutAgent(string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var userId = GetUserId();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Received Contact Center soft-phone sign-out request for user '{UserId}'.", userId);
        }

        await _presenceManager.SignOutAsync(userId);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Completed Contact Center soft-phone sign-out request for user '{UserId}'.", userId);
        }

        return RedirectToReturnLocation(returnUrl);
    }

    /// <summary>
    /// Sets the agent presence state from the soft phone widget.
    /// </summary>
    /// <param name="status">The presence state.</param>
    /// <param name="presenceReason">The optional reason code.</param>
    /// <param name="returnUrl">The local URL to return to after updating presence.</param>
    /// <returns>A redirect to the current page.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPresence(
        AgentPresenceStatus status,
        string presenceReason,
        string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        await _presenceManager.SetPresenceAsync(GetUserId(), status, presenceReason);

        return RedirectToReturnLocation(returnUrl);
    }

    private IActionResult RedirectToReturnLocation(string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (Request.Headers.Referer.Count > 0)
        {
            return Redirect(Request.Headers.Referer.ToString());
        }

        return LocalRedirect("~/");
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
