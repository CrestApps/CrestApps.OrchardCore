using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Mvc.Web.Areas.DataSources.Services;
using CrestApps.Core.Mvc.Web.Areas.DataSources.ViewModels;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.Controllers;

[Area("DataSources")]
[Authorize(Policy = "Admin")]
public sealed class AIDataSourceController : Controller
{
    private readonly ICatalogManager<AIDataSource> _manager;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IMvcAIDataSourceIndexingQueue _indexingQueue;
    private readonly TimeProvider _timeProvider;

    public AIDataSourceController(
        ICatalogManager<AIDataSource> manager,
        ISearchIndexProfileStore indexProfileStore,
        IMvcAIDataSourceIndexingQueue indexingQueue,
        TimeProvider timeProvider)
    {
        _manager = manager;
        _indexProfileStore = indexProfileStore;
        _indexingQueue = indexingQueue;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
    {
        var dataSources = await _manager.GetAllAsync();

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
        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        var dataSource = await _manager.NewAsync();
        dataSource.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        model.ApplyTo(dataSource);

        await _manager.CreateAsync(dataSource);
        TempData["SuccessMessage"] = "Data source created successfully. Initial synchronization has been queued.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var dataSource = await _manager.FindByIdAsync(id);

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
        var dataSource = await _manager.FindByIdAsync(model.ItemId);

        if (dataSource == null)
        {
            return NotFound();
        }

        await ValidateAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        model.ApplyTo(dataSource);

        await _manager.UpdateAsync(dataSource);
        TempData["SuccessMessage"] = "Data source updated successfully. Synchronization has been queued.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(string id)
    {
        var dataSource = await _manager.FindByIdAsync(id);

        if (dataSource == null)
        {
            return NotFound();
        }

        await _indexingQueue.QueueSyncDataSourceAsync(dataSource, HttpContext.RequestAborted);
        TempData["SuccessMessage"] = "Data source synchronization has been queued.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var dataSource = await _manager.FindByIdAsync(id);

        if (dataSource != null)
        {
            await _manager.DeleteAsync(dataSource);
            TempData["SuccessMessage"] = "Data source deleted successfully. Knowledge-base cleanup has been queued.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(AIDataSourceViewModel model)
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

        if (!string.IsNullOrWhiteSpace(model.SourceIndexProfileName))
        {
            var sourceIndexProfile = await _indexProfileStore.FindByNameAsync(model.SourceIndexProfileName);

            if (sourceIndexProfile == null)
            {
                ModelState.AddModelError(nameof(model.SourceIndexProfileName), "The selected source index profile could not be found.");
            }
            else if (string.Equals(sourceIndexProfile.Type, IndexProfileTypes.AIDocuments, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceIndexProfile.Type, IndexProfileTypes.AIMemory, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceIndexProfile.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.SourceIndexProfileName), "The selected source index profile type is not supported for data sources.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.AIKnowledgeBaseIndexProfileName))
        {
            var knowledgeBaseIndexProfile = await _indexProfileStore.FindByNameAsync(model.AIKnowledgeBaseIndexProfileName);

            if (knowledgeBaseIndexProfile == null)
            {
                ModelState.AddModelError(nameof(model.AIKnowledgeBaseIndexProfileName), "The selected knowledge base index profile could not be found.");
            }
            else if (!string.Equals(knowledgeBaseIndexProfile.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.AIKnowledgeBaseIndexProfileName), "The selected knowledge base index profile must be an AI Data Source profile.");
            }
        }
    }

    private async Task PopulateDropdownsAsync(AIDataSourceViewModel model)
    {
        var indexProfiles = await _indexProfileStore.GetAllAsync();

        // Source index profiles: exclude AI-specific target indexes.
        model.SourceIndexProfiles = indexProfiles
            .Where(p =>
                !string.Equals(p.Type, IndexProfileTypes.AIDocuments, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Type, IndexProfileTypes.AIMemory, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
            .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.Name))
            .ToList();

        // Knowledge base index profiles: only DataSource type profiles
        model.KnowledgeBaseIndexProfiles = indexProfiles
            .Where(p => string.Equals(p.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
            .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.Name))
            .ToList();
    }
}
