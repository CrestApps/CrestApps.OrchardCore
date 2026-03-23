using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class SettingsController : Controller
{
    private readonly JsonFileSettingsService _settingsService;

    public SettingsController(JsonFileSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _settingsService.GetAsync();

        var model = new SettingsViewModel
        {
            EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval,
            MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest,
            EnableDistributedCaching = settings.EnableDistributedCaching,
            EnableOpenTelemetry = settings.EnableOpenTelemetry,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel model)
    {
        if (model.MaximumIterationsPerRequest < 1)
        {
            ModelState.AddModelError(nameof(model.MaximumIterationsPerRequest), "Must be at least 1.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var settings = await _settingsService.GetAsync();

        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.MaximumIterationsPerRequest = model.MaximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        await _settingsService.SaveAsync(settings);

        TempData["SuccessMessage"] = "Settings saved successfully.";

        return RedirectToAction(nameof(Index));
    }
}
