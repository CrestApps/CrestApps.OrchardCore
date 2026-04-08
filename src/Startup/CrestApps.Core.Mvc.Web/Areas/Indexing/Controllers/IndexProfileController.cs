using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Mvc.Web.Areas.Indexing.ViewModels;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Areas.Indexing.Controllers;

[Area("Indexing")]
[Authorize(Policy = "Admin")]
public sealed class IndexProfileController : Controller
{
    private readonly ISearchIndexProfileManager _indexProfileManager;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexProfileController> _logger;
    private readonly IReadOnlyList<IndexProfileSourceDescriptor> _sources;

    public IndexProfileController(
        ISearchIndexProfileManager indexProfileManager,
        ICatalog<AIDeployment> deploymentCatalog,
        IServiceProvider serviceProvider,
        IOptions<IndexProfileSourceOptions> sourceOptions,
        ILogger<IndexProfileController> logger)
    {
        _indexProfileManager = indexProfileManager;
        _deploymentCatalog = deploymentCatalog;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _sources = sourceOptions.Value.Sources
            .OrderBy(source => source.ProviderDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _indexProfileManager.GetAllAsync();

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
        var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(profile.ProviderName);

        if (indexManager == null)
        {
            ModelState.AddModelError(nameof(model.ProviderName), "The selected search provider is not configured for remote index provisioning.");
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        profile.IndexFullName = indexManager.ComposeIndexFullName(profile);

        await ValidateHandlersAsync(profile);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        IReadOnlyCollection<SearchIndexField> fields;
        try
        {
            fields = await _indexProfileManager.GetFieldsAsync(profile, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.EmbeddingDeploymentId), ex.Message);
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        if (fields == null)
        {
            ModelState.AddModelError(nameof(model.Type), $"The index type '{profile.Type}' is not supported for remote provisioning.");
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        try
        {
            if (await indexManager.ExistsAsync(profile, HttpContext.RequestAborted))
            {
                ModelState.AddModelError(nameof(model.IndexName), $"The remote index '{profile.IndexFullName}' already exists.");
                await PopulateDropdownsAsync(model);

                return View(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to validate remote index '{IndexName}' for provider '{ProviderName}'.",
                profile.IndexFullName.SanitizeLogValue(),
                profile.ProviderName.SanitizeLogValue());
            ModelState.AddModelError(nameof(model.IndexName), $"Unable to validate whether the remote index '{profile.IndexFullName}' already exists.");
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        try
        {
            await indexManager.CreateAsync(profile, fields, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create remote index '{IndexName}' for provider '{ProviderName}'.",
                profile.IndexFullName.SanitizeLogValue(),
                profile.ProviderName.SanitizeLogValue());
            ModelState.AddModelError(nameof(model.IndexName), $"Unable to create the remote index '{profile.IndexFullName}'.");
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        try
        {
            await _indexProfileManager.CreateAsync(profile);
        }
        catch
        {
            await indexManager.DeleteAsync(profile, HttpContext.RequestAborted);
            throw;
        }

        await _indexProfileManager.SynchronizeAsync(profile, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var profile = await _indexProfileManager.FindByIdAsync(id);

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
        var profile = await _indexProfileManager.FindByIdAsync(model.ItemId);

        if (profile == null)
        {
            return NotFound();
        }

        await ValidateAsync(model, profile, profile.ItemId);

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        profile.DisplayText = model.DisplayText;

        await _indexProfileManager.UpdateAsync(profile);
        await _indexProfileManager.SynchronizeAsync(profile, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var profile = await _indexProfileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(profile.ProviderName ?? string.Empty);

        if (indexManager == null)
        {
            _logger.LogWarning(
                "Skipping remote delete for index profile '{IndexProfileId}' because provider '{ProviderName}' is not registered.",
                profile.ItemId.SanitizeLogValue(),
                profile.ProviderName.SanitizeLogValue());
            await _indexProfileManager.DeleteAsync(profile);

            return RedirectToAction(nameof(Index));
        }

        profile.IndexFullName ??= indexManager.ComposeIndexFullName(profile);

        bool remoteIndexExists;
        try
        {
            remoteIndexExists = await indexManager.ExistsAsync(profile, HttpContext.RequestAborted);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Skipping remote delete for index profile '{IndexProfileId}' because the resolved remote index name '{IndexName}' is invalid.",
                profile.ItemId.SanitizeLogValue(),
                profile.IndexFullName.SanitizeLogValue());
            remoteIndexExists = false;
        }

        if (remoteIndexExists)
        {
            try
            {
                await indexManager.DeleteAsync(profile, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete remote index '{IndexName}' for provider '{ProviderName}'. The local index profile was not removed.",
                    profile.IndexFullName.SanitizeLogValue(),
                    profile.ProviderName.SanitizeLogValue());
                TempData["ErrorMessage"] = $"Unable to delete the remote index '{profile.IndexFullName}'. The index profile was not removed.";

                return RedirectToAction(nameof(Index));
            }
        }

        await _indexProfileManager.DeleteAsync(profile);

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(IndexProfileViewModel model, SearchIndexProfile profile = null, string excludeItemId = null)
    {
        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            var existing = await _indexProfileManager.FindByNameAsync(model.Name.Trim());

            if (existing != null && existing.ItemId != excludeItemId)
            {
                ModelState.AddModelError(nameof(model.Name), "An index profile with this name already exists.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ProviderName) &&
            !string.IsNullOrWhiteSpace(model.Type) &&
            !_sources.Any(source =>
                string.Equals(source.ProviderName, model.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(source.Type, model.Type, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.Type), "The selected provider does not support this index type.");
        }

        if (profile == null)
        {
            return;
        }

        await ValidateHandlersAsync(profile);
    }

    private async Task ValidateHandlersAsync(SearchIndexProfile profile)
    {
        var validationResult = await _indexProfileManager.ValidateAsync(profile);

        foreach (var error in validationResult.Errors)
        {
            var memberNames = error.MemberNames?.Any() == true
                ? error.MemberNames
                : [string.Empty];

            foreach (var memberName in memberNames)
            {
                ModelState.AddModelError(memberName, error.ErrorMessage);
            }
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

        model.EmbeddingDeployments = deployments
            .Where(d => d.Type == AIDeploymentType.Embedding)
                .Select(d => new SelectListItem(d.Name, d.ItemId));
    }

}
