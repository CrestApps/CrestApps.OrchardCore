using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
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
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class SupervisorDashboardController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly HubRouteManager _hubRouteManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorDashboardController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="hubRouteManager">The hub route manager used to resolve the real-time hub URL.</param>
    public SupervisorDashboardController(
        IAuthorizationService authorizationService,
        HubRouteManager hubRouteManager)
    {
        _authorizationService = authorizationService;
        _hubRouteManager = hubRouteManager;
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

        var viewModel = new SupervisorDashboardIndexViewModel
        {
            HubUrl = _hubRouteManager.GetPathByHub<ContactCenterHub>(),
            StateUrl = Url.RouteUrl(SupervisorDashboardEndpoints.StateRouteName),
            EngageUrl = Url.RouteUrl(SupervisorDashboardEndpoints.EngageRouteName),
        };

        return View(viewModel);
    }
}
