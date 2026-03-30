using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class AITemplateController : Controller
{
    private readonly ICatalog<AIProfileTemplate> _catalog;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<A2AConnection> _a2aConnectionCatalog;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly AIToolDefinitionOptions _toolOptions;

    public AITemplateController(
        ICatalog<AIProfileTemplate> catalog,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<A2AConnection> a2aConnectionCatalog,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _catalog = catalog;
        _deploymentCatalog = deploymentCatalog;
        _a2aConnectionCatalog = a2aConnectionCatalog;
        _orchestratorOptions = orchestratorOptions.Value;
        _toolOptions = toolOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _catalog.GetAllAsync();

        return View(templates);
    }

    public async Task<IActionResult> Create()
    {
        var model = new AITemplateViewModel();
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AITemplateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Source is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var template = new AIProfileTemplate
        {
            ItemId = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
        };

        model.SelectedA2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        model.ApplyTo(template);

        await _catalog.CreateAsync(template);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var template = await _catalog.FindByIdAsync(id);

        if (template == null)
        {
            return NotFound();
        }

        var model = AITemplateViewModel.FromTemplate(template);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AITemplateViewModel model)
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

        var existing = await _catalog.FindByIdAsync(model.ItemId);

        if (existing == null)
        {
            return NotFound();
        }

        model.SelectedA2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        model.ApplyTo(existing);

        await _catalog.UpdateAsync(existing);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var template = await _catalog.FindByIdAsync(id);

        if (template == null)
        {
            return NotFound();
        }

        await _catalog.DeleteAsync(template);
        await _catalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(AITemplateViewModel model)
    {
        var allDeployments = await _deploymentCatalog.GetAllAsync();

        model.ChatDeployments = [new SelectListItem("— Default Chat Deployment —", "")];
        model.ChatDeployments.AddRange(allDeployments
            .Where(d => d.Type == AIDeploymentType.Chat)
            .Select(d => new SelectListItem(d.Name, d.ItemId)));

        model.UtilityDeployments = [new SelectListItem("— Default Utility Deployment —", "")];
        model.UtilityDeployments.AddRange(allDeployments
            .Where(d => d.Type == AIDeploymentType.Utility || d.Type == AIDeploymentType.Chat)
            .Select(d => new SelectListItem(d.Name, d.ItemId)));

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default Orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

        var selectedNames = new HashSet<string>(model.SelectedToolNames ?? [], StringComparer.OrdinalIgnoreCase);
        model.AvailableTools = _toolOptions.Tools
            .Where(kvp => !kvp.Value.IsSystemTool)
            .Select(kvp => new ToolSelectionItem
            {
                Name = kvp.Key,
                Title = kvp.Value.Title ?? kvp.Key,
                Description = kvp.Value.Description,
                Category = kvp.Value.Category ?? "Miscellaneous",
                IsSelected = selectedNames.Contains(kvp.Key),
            })
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Title)
            .ToList();

        var connections = await _a2aConnectionCatalog.GetAllAsync();
        var selectedConnectionIds = new HashSet<string>(model.SelectedA2AConnectionIds ?? [], StringComparer.Ordinal);
        model.AvailableA2AConnections = connections
            .OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(connection => new A2AConnectionSelectionItem
            {
                ItemId = connection.ItemId,
                DisplayText = connection.DisplayText,
                Endpoint = connection.Endpoint,
                IsSelected = selectedConnectionIds.Contains(connection.ItemId),
            })
            .ToList();
    }

    private async Task<string[]> GetValidA2AConnectionIdsAsync(IEnumerable<string> selectedIds)
    {
        var allIds = (await _a2aConnectionCatalog.GetAllAsync())
            .Select(connection => connection.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        return (selectedIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && allIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
