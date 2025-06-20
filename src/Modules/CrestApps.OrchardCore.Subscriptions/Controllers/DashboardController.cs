using CrestApps.OrchardCore.Subscriptions.Core.Models;
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

    public DashboardController(
        IDisplayManager<SubscriberDashboard> displayManager,
        IUpdateModelAccessor updateModelAccessor)
    {
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
    }

    [Admin("subscription-dashboard")]
    public async Task<IActionResult> Index()
    {
        var model = await _displayManager.BuildDisplayAsync(_updateModelAccessor.ModelUpdater);

        return View(model);
    }
}
