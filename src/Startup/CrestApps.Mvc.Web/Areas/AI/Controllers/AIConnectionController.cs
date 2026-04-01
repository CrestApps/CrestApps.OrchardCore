using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.AI.Controllers;

[Area("AI")]
[Authorize(Policy = "Admin")]
public sealed class AIConnectionController : Controller
{
    private readonly ICatalog<AIProviderConnection> _catalog;
    private readonly MvcAIProviderOptionsStore _providerOptionsStore;
    private readonly IOptionsMonitorCache<AIProviderOptions> _providerOptionsCache;
    private static readonly List<SelectListItem> _providers =
    [
        new("OpenAI", "OpenAI"),
        new("Azure OpenAI", "Azure"),
        new("Azure AI Inference (GitHub Models)", "AzureAIInference"),
        new("Ollama", "Ollama"),
    ];
    private static readonly List<SelectListItem> _authTypes =
    [
        new("API Key", "ApiKey"),
        new("Default Azure Credential", "Default"),
        new("Managed Identity", "ManagedIdentity"),
    ];
    public AIConnectionController(
        ICatalog<AIProviderConnection> catalog,
        MvcAIProviderOptionsStore providerOptionsStore,
        IOptionsMonitorCache<AIProviderOptions> providerOptionsCache)
    {
        _catalog = catalog;
        _providerOptionsStore = providerOptionsStore;
        _providerOptionsCache = providerOptionsCache;
    }

    public async Task<IActionResult> Index()
    {
        var connections = await _catalog.GetAllAsync();

        return View(connections);
    }

    public IActionResult Create()
    {
        var model = new AIConnectionViewModel
        {
            Providers = _providers,
            AuthenticationTypes = _authTypes,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AIConnectionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Provider is required.");
        }

        if (!ModelState.IsValid)
        {
            model.Providers = _providers;
            model.AuthenticationTypes = _authTypes;

            return View(model);
        }

        var connection = new AIProviderConnection();

        if (string.IsNullOrEmpty(connection.ItemId))
        {
            connection.ItemId = Guid.NewGuid().ToString("N");
        }

        connection.CreatedUtc = DateTime.UtcNow;
        model.ApplyTo(connection);
        await _catalog.CreateAsync(connection);
        await _catalog.SaveChangesAsync();
        await RefreshProviderOptionsAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var connection = await _catalog.FindByIdAsync(id);

        if (connection == null)
        {
            return NotFound();
        }

        var model = AIConnectionViewModel.FromConnection(connection);
        model.Providers = _providers;
        model.AuthenticationTypes = _authTypes;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AIConnectionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (!ModelState.IsValid)
        {
            model.Providers = _providers;
            model.AuthenticationTypes = _authTypes;

            return View(model);
        }

        var existing = await _catalog.FindByIdAsync(model.ItemId);

        if (existing == null)
        {
            return NotFound();
        }

        model.ApplyTo(existing);
        await _catalog.UpdateAsync(existing);
        await _catalog.SaveChangesAsync();
        await RefreshProviderOptionsAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var connection = await _catalog.FindByIdAsync(id);

        if (connection == null)
        {
            return NotFound();
        }

        await _catalog.DeleteAsync(connection);
        await _catalog.SaveChangesAsync();
        await RefreshProviderOptionsAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task RefreshProviderOptionsAsync()
    {
        _providerOptionsStore.Replace(await _catalog.GetAllAsync());
        _providerOptionsCache.TryRemove(Options.DefaultName);
    }
}
