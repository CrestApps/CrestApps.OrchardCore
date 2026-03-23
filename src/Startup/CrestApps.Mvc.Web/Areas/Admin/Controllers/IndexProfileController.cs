using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class IndexProfileController : Controller
{
    private readonly ISearchIndexProfileStore _store;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;

    private static readonly List<SelectListItem> _providers =
    [
        new("Elasticsearch", "Elasticsearch"),
        new("Azure AI Search", "AzureAISearch"),
    ];

    private static readonly List<SelectListItem> _types =
    [
        new("AI Documents", IndexProfileTypes.AIDocuments),
        new("Data Source", IndexProfileTypes.DataSource),
        new("AI Memory", IndexProfileTypes.AIMemory),
    ];

    public IndexProfileController(
        ISearchIndexProfileStore store,
        ICatalog<AIDeployment> deploymentCatalog)
    {
        _store = store;
        _deploymentCatalog = deploymentCatalog;
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _store.GetAllAsync();

        return View(profiles);
    }

    public async Task<IActionResult> Create()
    {
        var model = new IndexProfileViewModel();
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IndexProfileViewModel model)
    {
        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var profile = new SearchIndexProfile();
        model.ApplyTo(profile);

        await _store.CreateAsync(profile);
        await _store.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var profile = await _store.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var model = IndexProfileViewModel.FromProfile(profile);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IndexProfileViewModel model)
    {
        var profile = await _store.FindByIdAsync(model.ItemId);

        if (profile == null)
        {
            return NotFound();
        }

        await ValidateAsync(model, profile.ItemId);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        model.ApplyTo(profile);

        await _store.UpdateAsync(profile);
        await _store.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var profile = await _store.FindByIdAsync(id);

        if (profile != null)
        {
            await _store.DeleteAsync(profile);
            await _store.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(IndexProfileViewModel model, string excludeItemId = null)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }
        else
        {
            var existing = await _store.FindByNameAsync(model.Name.Trim());

            if (existing != null && existing.ItemId != excludeItemId)
            {
                ModelState.AddModelError(nameof(model.Name), "An index profile with this name already exists.");
            }
        }

        if (string.IsNullOrWhiteSpace(model.IndexName))
        {
            ModelState.AddModelError(nameof(model.IndexName), "Index name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.ProviderName))
        {
            ModelState.AddModelError(nameof(model.ProviderName), "Provider is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Type))
        {
            ModelState.AddModelError(nameof(model.Type), "Type is required.");
        }
    }

    private async Task PopulateDropdownsAsync(IndexProfileViewModel model)
    {
        model.Providers = _providers;
        model.Types = _types;

        var deployments = await _deploymentCatalog.GetAllAsync();

        model.EmbeddingDeployments = [new SelectListItem("— None —", "")];
        model.EmbeddingDeployments.AddRange(
            deployments
                .Where(d => d.Type == AIDeploymentType.Embedding)
                .Select(d => new SelectListItem(d.Name, d.ItemId)));
    }
}
