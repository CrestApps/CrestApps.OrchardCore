using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Mvc.Web.Areas.AI.Services;
using CrestApps.Mvc.Web.Areas.AI.ViewModels;
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
    private readonly IConfiguration _configuration;
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
        IConfiguration configuration,
        MvcAIProviderOptionsStore providerOptionsStore,
        IOptionsMonitorCache<AIProviderOptions> providerOptionsCache)
    {
        _catalog = catalog;
        _configuration = configuration;
        _providerOptionsStore = providerOptionsStore;
        _providerOptionsCache = providerOptionsCache;
    }

    public async Task<IActionResult> Index()
    {
        var connections = await _catalog.GetAllAsync();
        var configuredConnections = GetConfiguredConnections();
        var configuredKeys = configuredConnections.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var models = connections
            .Select(connection =>
            {
                var model = AIConnectionViewModel.FromConnection(connection);
                model.IsReadOnly = configuredKeys.Contains(BuildConnectionKey(connection.Source, connection.Name));
                return model;
            })
            .ToList();

        var existingKeys = models
            .Select(static model => BuildConnectionKey(model.Source, model.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, connection) in configuredConnections)
        {
            if (existingKeys.Contains(key))
            {
                continue;
            }

            models.Add(AIConnectionViewModel.FromConfiguration(
                AIConfigurationRecordIds.CreateConnectionId(connection.ProviderName, connection.ConnectionName),
                connection.ConnectionName,
                connection.DisplayText,
                connection.ProviderName));
        }

        return View(models
            .OrderBy(static model => model.DisplayText ?? model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
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
        if (AIConfigurationRecordIds.IsConfigurationConnectionId(id))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be edited from the UI.";
            return RedirectToAction(nameof(Index));
        }

        var connection = await _catalog.FindByIdAsync(id);

        if (connection == null)
        {
            return NotFound();
        }

        if (IsConfigurationBacked(connection))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be edited from the UI.";
            return RedirectToAction(nameof(Index));
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
        if (AIConfigurationRecordIds.IsConfigurationConnectionId(model.ItemId))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be edited from the UI.";
            return RedirectToAction(nameof(Index));
        }

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

        if (IsConfigurationBacked(existing))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be edited from the UI.";
            return RedirectToAction(nameof(Index));
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
        if (AIConfigurationRecordIds.IsConfigurationConnectionId(id))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be deleted from the UI.";
            return RedirectToAction(nameof(Index));
        }

        var connection = await _catalog.FindByIdAsync(id);

        if (connection == null)
        {
            return NotFound();
        }

        if (IsConfigurationBacked(connection))
        {
            TempData["ErrorMessage"] = "Connections defined in appsettings are read-only and cannot be deleted from the UI.";
            return RedirectToAction(nameof(Index));
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

    private bool IsConfigurationBacked(AIProviderConnection connection)
        => GetConfiguredConnections().ContainsKey(BuildConnectionKey(connection.Source, connection.Name));

    private Dictionary<string, ConfiguredConnectionEntry> GetConfiguredConnections()
    {
        var connections = new Dictionary<string, ConfiguredConnectionEntry>(StringComparer.OrdinalIgnoreCase);

        ReadTopLevelConnections(connections);
        ReadProviderConnections("CrestApps:Providers", connections);
        ReadProviderConnections("CrestApps:AI:Providers", connections);

        return connections;
    }

    private void ReadTopLevelConnections(Dictionary<string, ConfiguredConnectionEntry> connections)
    {
        var section = _configuration.GetSection("CrestApps:AI:Connections");

        if (!section.Exists())
        {
            return;
        }

        foreach (var connectionSection in section.GetChildren())
        {
            var connectionName = connectionSection["Name"];
            var providerName = AIProviderNameNormalizer.Normalize(connectionSection["ClientName"]);

            if (string.IsNullOrWhiteSpace(connectionName) || string.IsNullOrWhiteSpace(providerName))
            {
                continue;
            }

            var displayText = connectionSection["ConnectionNameAlias"];

            AddConfiguredConnection(connections, providerName, connectionName, displayText);
        }
    }

    private void ReadProviderConnections(string sectionPath, Dictionary<string, ConfiguredConnectionEntry> connections)
    {
        var section = _configuration.GetSection(sectionPath);

        if (!section.Exists())
        {
            return;
        }

        foreach (var providerSection in section.GetChildren())
        {
            var connectionsSection = providerSection.GetSection("Connections");

            if (!connectionsSection.Exists())
            {
                continue;
            }

            foreach (var connectionSection in connectionsSection.GetChildren())
            {
                if (string.IsNullOrWhiteSpace(connectionSection.Key))
                {
                    continue;
                }

                AddConfiguredConnection(
                    connections,
                    AIProviderNameNormalizer.Normalize(providerSection.Key),
                    connectionSection.Key,
                    connectionSection["ConnectionNameAlias"]);
            }
        }
    }

    private static void AddConfiguredConnection(
        Dictionary<string, ConfiguredConnectionEntry> connections,
        string providerName,
        string connectionName,
        string displayText)
    {
        providerName = AIProviderNameNormalizer.Normalize(providerName);
        var key = BuildConnectionKey(providerName, connectionName);
        connections[key] = new ConfiguredConnectionEntry(
            providerName,
            connectionName,
            string.IsNullOrWhiteSpace(displayText) ? connectionName : displayText);
    }

    private static string BuildConnectionKey(string providerName, string connectionName)
        => $"{AIProviderNameNormalizer.Normalize(providerName)}:{connectionName}";

    private sealed record ConfiguredConnectionEntry(string ProviderName, string ConnectionName, string DisplayText);
}
