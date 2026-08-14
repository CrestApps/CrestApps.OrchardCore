using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

[Admin]
public class DashboardController : Controller
{
    private readonly IDisplayManager<SubscriberDashboard> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;

    public DashboardController(
        IDisplayManager<SubscriberDashboard> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService)
    {
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
    }

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
