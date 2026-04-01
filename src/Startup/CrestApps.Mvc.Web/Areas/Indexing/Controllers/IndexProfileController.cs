using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Models;
using CrestApps.Mvc.Web.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.Indexing.Controllers;

[Area("Indexing")]
[Authorize(Policy = "Admin")]
public sealed class IndexProfileController : Controller
{
    private readonly ISearchIndexProfileStore _store;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly IEnumerable<IIndexProfileHandler> _handlers;
    private readonly IReadOnlyList<IndexProfileSourceDescriptor> _sources;

    public IndexProfileController(
        ISearchIndexProfileStore store,
        ICatalog<AIDeployment> deploymentCatalog,
        IEnumerable<IIndexProfileHandler> handlers,
        IOptions<IndexProfileSourceOptions> sourceOptions)
    {
        _store = store;
        _deploymentCatalog = deploymentCatalog;
        _handlers = handlers;
        _sources = sourceOptions.Value.Sources
            .OrderBy(source => source.ProviderDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        // Auto-generate Name from IndexName when not provided.

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = model.IndexName?.Trim();
        }

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
        await NotifySynchronizedAsync(profile);

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
        await NotifySynchronizedAsync(profile);

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
        else if (!_sources.Any(source =>
        string.Equals(source.ProviderName, model.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(source.Type, model.Type, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.Type), "The selected provider does not support this index type.");
        }
    }

    private async Task PopulateDropdownsAsync(IndexProfileViewModel model)
    {
        model.Sources = _sources;
        model.Providers = _sources
            .GroupBy(source => source.ProviderName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(source => new SelectListItem(source.ProviderDisplayName, source.ProviderName))
            .ToList();
        model.Types = _sources
            .GroupBy(source => source.Type, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(source => new SelectListItem(source.DisplayName, source.Type))
            .ToList();

        var deployments = await _deploymentCatalog.GetAllAsync();

        model.EmbeddingDeployments = [new SelectListItem("— None —", "")];
        model.EmbeddingDeployments.AddRange(
            deployments
            .Where(d => d.Type == AIDeploymentType.Embedding)
                .Select(d => new SelectListItem(d.Name, d.ItemId)));
    }

    private async Task NotifySynchronizedAsync(SearchIndexProfile profile)
    {
        foreach (var handler in _handlers)
        {
            await handler.SynchronizedAsync(profile);
        }
    }
}
