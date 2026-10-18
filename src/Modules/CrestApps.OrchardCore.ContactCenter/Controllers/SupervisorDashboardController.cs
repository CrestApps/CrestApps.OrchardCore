using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Serves the supervisor dashboard page used by managers to monitor operations in real time.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Supervision)]
public sealed class SupervisorDashboardController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorDashboardController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="supervisorQueueAuthorizationService">The service that decides which queues a supervisor may see.</param>
    public SupervisorDashboardController(
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService)
    {
        _authorizationService = authorizationService;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
    }

    /// <summary>
    /// Renders the supervisor dashboard page.
    /// </summary>
    /// <returns>The supervisor dashboard view.</returns>
    [Admin("contact-center/dashboard", "ContactCenterSupervisorDashboard")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter))
        {
            return Forbid();
        }

        // The page opens with the dialer's state so a stalled dialer is the first thing a supervisor sees; the page then
        // keeps it current from the dialer endpoint. Without Paced Dialing there is no banner.
        var dialerBanner = await SupervisorDashboardEndpoints.BuildDialerBannerAsync(_supervisorQueueAuthorizationService, HttpContext);

        var viewModel = new SupervisorDashboardIndexViewModel
        {
            HubUrl = SignalRHubRoutes.GetTenantAwareHubUrl<ContactCenterHub>(HttpContext),
            StateUrl = Url.RouteUrl(SupervisorDashboardEndpoints.StateRouteName),
            EngageUrl = Url.RouteUrl(SupervisorDashboardEndpoints.EngageRouteName),
            DispatchUrl = Url.RouteUrl(SupervisorDashboardEndpoints.DispatchRouteName),
            DialerUrl = dialerBanner is null ? null : Url.RouteUrl(SupervisorDashboardEndpoints.DialerRouteName),
            DialerBanner = dialerBanner,
            InterventionUrls = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stop"] = Url.RouteUrl(SupervisorInterventionEndpoints.StopRouteName),
                ["switch"] = Url.RouteUrl(SupervisorInterventionEndpoints.SwitchRouteName),
                ["takeover"] = Url.RouteUrl(SupervisorInterventionEndpoints.TakeOverRouteName),
                ["endCall"] = Url.RouteUrl(SupervisorInterventionEndpoints.EndCallRouteName),
                ["transfer"] = Url.RouteUrl(SupervisorInterventionEndpoints.TransferRouteName),
                ["recording"] = Url.RouteUrl(SupervisorInterventionEndpoints.RecordingRouteName),
                ["agentState"] = Url.RouteUrl(SupervisorInterventionEndpoints.AgentStateRouteName),
                ["message"] = Url.RouteUrl(SupervisorInterventionEndpoints.MessageRouteName),
                ["freeAgent"] = Url.RouteUrl(SupervisorInterventionEndpoints.FreeAgentRouteName),
            },
        };

        return View(viewModel);
    }
}
