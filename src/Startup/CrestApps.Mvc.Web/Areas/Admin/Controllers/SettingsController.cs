using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class SettingsController : Controller
{
    private readonly JsonFileSettingsService _settingsService;
    private readonly JsonFileDeploymentDefaultsService _deploymentDefaultsService;
    private readonly IAIDeploymentManager _deploymentManager;

    public SettingsController(
        JsonFileSettingsService settingsService,
        JsonFileDeploymentDefaultsService deploymentDefaultsService,
        IAIDeploymentManager deploymentManager)
    {
        _settingsService = settingsService;
        _deploymentDefaultsService = deploymentDefaultsService;
        _deploymentManager = deploymentManager;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _settingsService.GetAsync();
        var deploymentDefaults = await _deploymentDefaultsService.GetAsync();

        var model = new SettingsViewModel
        {
            EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval,
            MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest,
            EnableDistributedCaching = settings.EnableDistributedCaching,
            EnableOpenTelemetry = settings.EnableOpenTelemetry,

            DefaultChatDeploymentId = deploymentDefaults.DefaultChatDeploymentId,
            DefaultUtilityDeploymentId = deploymentDefaults.DefaultUtilityDeploymentId,
            DefaultEmbeddingDeploymentId = deploymentDefaults.DefaultEmbeddingDeploymentId,
            DefaultImageDeploymentId = deploymentDefaults.DefaultImageDeploymentId,
            DefaultSpeechToTextDeploymentId = deploymentDefaults.DefaultSpeechToTextDeploymentId,
            DefaultTextToSpeechDeploymentId = deploymentDefaults.DefaultTextToSpeechDeploymentId,
            DefaultTextToSpeechVoiceId = deploymentDefaults.DefaultTextToSpeechVoiceId,
        };

        await PopulateDeploymentDropdownsAsync(model);

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
            await PopulateDeploymentDropdownsAsync(model);

            return View(nameof(Index), model);
        }

        // Save general AI settings.
        var settings = await _settingsService.GetAsync();

        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.MaximumIterationsPerRequest = model.MaximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        await _settingsService.SaveAsync(settings);

        // Save default deployment settings.
        var deploymentDefaults = new DefaultAIDeploymentSettings
        {
            DefaultChatDeploymentId = model.DefaultChatDeploymentId,
            DefaultUtilityDeploymentId = model.DefaultUtilityDeploymentId,
            DefaultEmbeddingDeploymentId = model.DefaultEmbeddingDeploymentId,
            DefaultImageDeploymentId = model.DefaultImageDeploymentId,
            DefaultSpeechToTextDeploymentId = model.DefaultSpeechToTextDeploymentId,
            DefaultTextToSpeechDeploymentId = model.DefaultTextToSpeechDeploymentId,
            DefaultTextToSpeechVoiceId = model.DefaultTextToSpeechVoiceId?.Trim(),
        };

        await _deploymentDefaultsService.SaveAsync(deploymentDefaults);

        TempData["SuccessMessage"] = "Settings saved successfully.";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDeploymentDropdownsAsync(SettingsViewModel model)
    {
        model.ChatDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

        model.UtilityDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));

        model.EmbeddingDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding));

        model.ImageDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.Image));

        model.SpeechToTextDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.SpeechToText));

        model.TextToSpeechDeployments = BuildGroupedDeploymentItems(
            await _deploymentManager.GetByTypeAsync(AIDeploymentType.TextToSpeech));
    }

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionNameAlias ?? d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                SelectListGroup group = null;
                var groupKey = d.ConnectionNameAlias ?? d.ConnectionName;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                return new SelectListItem(d.Name, d.ItemId) { Group = group };
            });
    }
}
