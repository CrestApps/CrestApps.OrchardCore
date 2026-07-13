using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Serves the CRM-integrated agent desktop page where an agent spends the shift.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class AgentWorkspaceController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly HubRouteManager _hubRouteManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkspaceController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="reasonCodeManager">The agent state reason code manager used to build presence options.</param>
    /// <param name="userManager">The user manager used to resolve the current Orchard user.</param>
    /// <param name="displayNameProvider">The display name provider used to render the agent's full name.</param>
    /// <param name="hubRouteManager">The hub route manager used to resolve the real-time hub URL.</param>
    public AgentWorkspaceController(
        IAuthorizationService authorizationService,
        IAgentStateReasonCodeManager reasonCodeManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        HubRouteManager hubRouteManager)
    {
        _authorizationService = authorizationService;
        _reasonCodeManager = reasonCodeManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _hubRouteManager = hubRouteManager;
    }

    /// <summary>
    /// Renders the agent desktop page.
    /// </summary>
    /// <returns>The agent workspace view.</returns>
    [Admin("contact-center/workspace", "ContactCenterAgentWorkspace")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var reasonCodes = await _reasonCodeManager.ListEnabledAsync();
        var displayName = await GetCurrentUserDisplayNameAsync(HttpContext.RequestAborted);

        var viewModel = new AgentWorkspaceIndexViewModel
        {
            DisplayName = displayName,
            CanMonitor = await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter),
            HubUrl = _hubRouteManager.GetPathByHub<ContactCenterHub>(),
            StateUrl = Url.RouteUrl(AgentWorkspaceEndpoints.StateRouteName),
            SetPresenceUrl = Url.RouteUrl(AgentWorkspaceEndpoints.SetPresenceRouteName),
            AcceptOfferUrl = Url.RouteUrl(VoiceOfferEndpoints.AcceptOfferRouteName),
            DeclineOfferUrl = Url.RouteUrl(VoiceOfferEndpoints.DeclineOfferRouteName),
            SupervisorDashboardUrl = Url.Action(nameof(SupervisorDashboardController.Index), "SupervisorDashboard"),
            ReasonCodes = [.. reasonCodes.Select(code => new WorkspaceLookupViewModel
            {
                Id = code.AppliesTo.ToString(),
                Name = code.Name,
            })],
        };

        return View(viewModel);
    }

    private async Task<string> GetCurrentUserDisplayNameAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is not null)
        {
            return await GetUserDisplayNameAsync(user, "Unknown user", cancellationToken);
        }

        return "Unknown user";
    }

    private async Task<string> GetUserDisplayNameAsync(
        IUser user,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (user is not null)
        {
            var displayName = await _displayNameProvider.GetAsync(user, cancellationToken);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }

        return fallback;
    }
}
