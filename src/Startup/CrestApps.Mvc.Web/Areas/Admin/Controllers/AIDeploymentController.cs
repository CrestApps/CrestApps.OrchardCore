using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class AIDeploymentController : Controller
{
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<AIProviderConnection> _connectionCatalog;

    private static readonly List<SelectListItem> _providers =
    [
        new("OpenAI", "OpenAI"),
        new("Azure OpenAI", "Azure"),
        new("Azure AI Inference (GitHub Models)", "AzureAIInference"),
        new("Ollama", "Ollama"),
    ];

    public AIDeploymentController(
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<AIProviderConnection> connectionCatalog)
    {
        _deploymentCatalog = deploymentCatalog;
        _connectionCatalog = connectionCatalog;
    }

    public async Task<IActionResult> Index()
    {
        var deployments = await _deploymentCatalog.GetAllAsync();

        return View(deployments);
    }

    public async Task<IActionResult> Create()
    {
        var model = new AIDeploymentViewModel();
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AIDeploymentViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var deployment = new AIDeployment
        {
            ItemId = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
        };

        model.ApplyTo(deployment);

        await _deploymentCatalog.CreateAsync(deployment);
        await _deploymentCatalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var deployment = await _deploymentCatalog.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        var model = AIDeploymentViewModel.FromDeployment(deployment);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AIDeploymentViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var existing = await _deploymentCatalog.FindByIdAsync(model.ItemId);

        if (existing == null)
        {
            return NotFound();
        }

        model.ApplyTo(existing);

        await _deploymentCatalog.UpdateAsync(existing);
        await _deploymentCatalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var deployment = await _deploymentCatalog.FindByIdAsync(id);

        if (deployment == null)
        {
            return NotFound();
        }

        await _deploymentCatalog.DeleteAsync(deployment);
        await _deploymentCatalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(AIDeploymentViewModel model)
    {
        var connections = await _connectionCatalog.GetAllAsync();
        model.Connections = [new SelectListItem("— Select Connection —", "")];
        model.Connections.AddRange(connections.Select(c => new SelectListItem(c.DisplayText ?? c.Name, c.Name)));
        model.Providers = _providers;
    }
}
