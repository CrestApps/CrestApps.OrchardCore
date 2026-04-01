using CrestApps.AI.DataSources;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.DataSources.Controllers;

[Area("DataSources")]
[Authorize(Policy = "Admin")]
public sealed class AIDataSourceController : Controller
{
    private readonly IAIDataSourceStore _store;
    private readonly ISearchIndexProfileStore _indexProfileStore;

    public AIDataSourceController(
        IAIDataSourceStore store,
        ISearchIndexProfileStore indexProfileStore)
    {
        _store = store;
        _indexProfileStore = indexProfileStore;
    }

    public async Task<IActionResult> Index()
    {
        var dataSources = await _store.GetAllAsync();

        return View(dataSources);
    }

    public async Task<IActionResult> Create()
    {
        var model = new AIDataSourceViewModel();
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AIDataSourceViewModel model)
    {
        Validate(model);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        var dataSource = new AIDataSource
        {
            CreatedUtc = DateTime.UtcNow,
        };

        model.ApplyTo(dataSource);

        await _store.CreateAsync(dataSource);
        await _store.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var dataSource = await _store.FindByIdAsync(id);

        if (dataSource == null)
        {
            return NotFound();
        }

        var model = AIDataSourceViewModel.FromDataSource(dataSource);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AIDataSourceViewModel model)
    {
        var dataSource = await _store.FindByIdAsync(model.ItemId);

        if (dataSource == null)
        {
            return NotFound();
        }

        Validate(model);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        model.ApplyTo(dataSource);

        await _store.UpdateAsync(dataSource);
        await _store.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var dataSource = await _store.FindByIdAsync(id);

        if (dataSource != null)
        {
            await _store.DeleteAsync(dataSource);
            await _store.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private void Validate(AIDataSourceViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            ModelState.AddModelError(nameof(model.DisplayText), "Display text is required.");
        }

        if (string.IsNullOrWhiteSpace(model.SourceIndexProfileName))
        {
            ModelState.AddModelError(nameof(model.SourceIndexProfileName), "Source index profile is required.");
        }

        if (string.IsNullOrWhiteSpace(model.ContentFieldName))
        {
            ModelState.AddModelError(nameof(model.ContentFieldName), "Content field name is required.");
        }
    }

    private async Task PopulateDropdownsAsync(AIDataSourceViewModel model)
    {
        var indexProfiles = await _indexProfileStore.GetAllAsync();

        // Source index profiles: exclude DataSource type profiles
        model.SourceIndexProfiles = [new SelectListItem("— Select index profile —", "")];
        model.SourceIndexProfiles.AddRange(
            indexProfiles
            .Where(p => !string.Equals(p.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
                .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.Name)));

        // Knowledge base index profiles: only DataSource type profiles
        model.KnowledgeBaseIndexProfiles = [new SelectListItem("— Select index profile —", "")];
        model.KnowledgeBaseIndexProfiles.AddRange(
            indexProfiles
            .Where(p => string.Equals(p.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
                .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.Name)));
    }
}
