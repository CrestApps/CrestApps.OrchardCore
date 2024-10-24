using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

public class DashboardController : Controller
{
    private readonly DisplayManager<SubscriberDashboard> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    public DashboardController(
        DisplayManager<SubscriberDashboard> displayManager,
        IUpdateModelAccessor updateModelAccessor)
    {
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
    }

    public async Task<IActionResult> Index()
    {
        var model = await _displayManager.BuildDisplayAsync(_updateModelAccessor.ModelUpdater);

        return View(model);
    }
}
