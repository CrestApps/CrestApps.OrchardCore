using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Displays the current subscriber's dashboard in the admin area.
/// </summary>
[Admin]
public class DashboardController : Controller
{
    private readonly IDisplayManager<SubscriberDashboard> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="displayManager">The display manager used to build the subscriber dashboard shape.</param>
    /// <param name="updateModelAccessor">The accessor that provides the current model updater.</param>
    /// <param name="authorizationService">The authorization service used to check dashboard access.</param>
    public DashboardController(
        IDisplayManager<SubscriberDashboard> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService)
    {
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Displays the subscriber dashboard for the current user.
    /// </summary>
    /// <returns>The dashboard view, or a forbidden result when access is denied.</returns>
    [Admin("subscription-dashboard")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var model = await _displayManager.BuildDisplayAsync(_updateModelAccessor.ModelUpdater);

        return View(model);
    }
}
