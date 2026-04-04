using CrestApps.AI.A2A.Models;
using CrestApps.AI.Copilot.Models;
using CrestApps.AI.Copilot.Services;
using CrestApps.AI.DataSources;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Profiles;
using CrestApps.AI.Tooling;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.A2A.ViewModels;
using CrestApps.Mvc.Web.Areas.AI.ViewModels;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.ChatInteractions.ViewModels;
using CrestApps.Mvc.Web.Areas.Mcp.ViewModels;
using CrestApps.Services;
using CrestApps.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.AI.Controllers;

[Area("AI")]
[Authorize(Policy = "Admin")]
public sealed class AITemplateController : Controller
{
    private readonly ICatalog<AIProfileTemplate> _catalog;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<A2AConnection> _a2aConnectionCatalog;
    private readonly ICatalog<McpConnection> _mcpConnectionCatalog;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIProfileManager _profileManager;
    private readonly InteractionDocumentOptions _interactionDocumentOptions;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly ITemplateService _aiTemplateService;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly CopilotOptions _copilotOptions;
    private readonly GitHubOAuthService _oauthService;

    private readonly AIToolDefinitionOptions _toolOptions;

    public AITemplateController(
        ICatalog<AIProfileTemplate> catalog,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<A2AConnection> a2aConnectionCatalog,
        ICatalog<McpConnection> mcpConnectionCatalog,
        IAIDataSourceStore dataSourceStore,
        IAIProfileManager profileManager,
        IOptions<InteractionDocumentOptions> interactionDocumentOptions,
        ISearchIndexProfileStore indexProfileStore,
        ITemplateService aiTemplateService,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<CopilotOptions> copilotOptions,
        GitHubOAuthService oauthService,
        IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _catalog = catalog;
        _deploymentCatalog = deploymentCatalog;
        _a2aConnectionCatalog = a2aConnectionCatalog;
        _mcpConnectionCatalog = mcpConnectionCatalog;
        _dataSourceStore = dataSourceStore;
        _profileManager = profileManager;
        _interactionDocumentOptions = interactionDocumentOptions.Value;
        _indexProfileStore = indexProfileStore;
        _aiTemplateService = aiTemplateService;
        _orchestratorOptions = orchestratorOptions.Value;
        _copilotOptions = copilotOptions.Value;
        _oauthService = oauthService;
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
        model.SelectedMcpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        model.SelectedAgentNames = await GetValidAgentNamesAsync(model.SelectedAgentNames);

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
        await NormalizeDeploymentSelectorsAsync(model);

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
        model.SelectedMcpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        model.SelectedAgentNames = await GetValidAgentNamesAsync(model.SelectedAgentNames);

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

        model.ChatDeployments = allDeployments
            .Where(d => d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name))
            .ToList();

        model.UtilityDeployments = allDeployments
            .Where(d => d.Type.Supports(AIDeploymentType.Utility) || d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name))
            .ToList();

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = orchestrators
            .Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key))
            .ToList();

        model.CopilotAuthenticationType = _copilotOptions.AuthenticationType;
        model.CopilotIsConfigured = IsCopilotConfigured();

        await PopulateCopilotStatusAsync(model);

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

        var mcpConnections = await _mcpConnectionCatalog.GetAllAsync();
        var selectedMcpIds = new HashSet<string>(model.SelectedMcpConnectionIds ?? [], StringComparer.Ordinal);
        model.AvailableMcpConnections = mcpConnections
            .OrderBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(c => new McpConnectionSelectionItem
            {
                ItemId = c.ItemId,
                DisplayText = c.DisplayText,
                Source = c.Source,
                IsSelected = selectedMcpIds.Contains(c.ItemId),
            })

        .ToList();

        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];
        var selectedAgentNames = new HashSet<string>(model.SelectedAgentNames ?? [], StringComparer.OrdinalIgnoreCase);
        model.AvailableAgents = allAgents
            .Where(a => !string.IsNullOrEmpty(a.Description))
            .OrderBy(a => a.DisplayText ?? a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AgentSelectionItem
            {
                Name = a.Name,
                DisplayText = a.DisplayText ?? a.Name,
                Description = a.Description,
                IsSelected = selectedAgentNames.Contains(a.Name),
            })
        .ToList();

        var promptTemplates = await _aiTemplateService.ListAsync();
        model.AvailablePromptTemplates = promptTemplates
            .Where(t => t.Metadata.IsListable)
            .OrderBy(t => t.Metadata.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Metadata.Title ?? t.Id, StringComparer.OrdinalIgnoreCase)
            .Select(t => new PromptTemplateOptionItem
            {
                TemplateId = t.Id,
                Title = t.Metadata.Title ?? t.Id,
                Description = t.Metadata.Description,
                Category = t.Metadata.Category ?? "General",
                Parameters = (t.Metadata.Parameters ?? []).Select(p => new PromptTemplateParameterItem
                {
                    Name = p.Name,
                    Description = p.Description,
                }).ToList(),
            })
            .ToList();

        var documentSettings = _interactionDocumentOptions;
        model.DocumentIndexProfileName = documentSettings.IndexProfileName;

        if (!string.IsNullOrWhiteSpace(documentSettings.IndexProfileName))
        {
            var documentIndexProfile = await _indexProfileStore.FindByNameAsync(documentSettings.IndexProfileName);
            model.HasDocumentIndexConfiguration = documentIndexProfile != null &&
                string.Equals(documentIndexProfile.Type, IndexProfileTypes.AIDocuments, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            model.HasDocumentIndexConfiguration = false;
        }

        var dataSources = await _dataSourceStore.GetAllAsync();
        model.DataSources = dataSources
            .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId))
            .ToList();
    }

    private async Task<string[]> GetValidAgentNamesAsync(IEnumerable<string> selectedNames)
    {
        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];
        var validNames = allAgents
            .Select(a => a.Name)

            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (selectedNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name) && validNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private async Task<string[]> GetValidMcpConnectionIdsAsync(IEnumerable<string> selectedIds)
    {
        var allIds = (await _mcpConnectionCatalog.GetAllAsync())
            .Select(c => c.ItemId)

            .ToHashSet(StringComparer.Ordinal);

        return (selectedIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && allIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    }

    private async Task NormalizeDeploymentSelectorsAsync(AITemplateViewModel model)
    {
        model.ChatDeploymentName = await NormalizeDeploymentSelectorAsync(model.ChatDeploymentName);
        model.UtilityDeploymentName = await NormalizeDeploymentSelectorAsync(model.UtilityDeploymentName);

    }

    private async Task<string> NormalizeDeploymentSelectorAsync(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return selector;

        }

        var deployment = await _deploymentCatalog.FindByIdAsync(selector);

        return deployment?.Name ?? selector;

    }

    private static string BuildDeploymentLabel(AIDeployment deployment)
        => string.Equals(deployment.Name, deployment.ModelName, StringComparison.OrdinalIgnoreCase)
    ? deployment.Name

    : $"{deployment.Name} ({deployment.ModelName})";

    private async Task PopulateCopilotStatusAsync(AITemplateViewModel model)
    {
        if (_copilotOptions.AuthenticationType != CopilotAuthenticationType.GitHubOAuth)
        {
            return;

        }

        var userId = User.Identity?.Name;

        if (string.IsNullOrEmpty(userId))
        {
            return;

        }

        var isAuth = await _oauthService.IsAuthenticatedAsync(userId);
        model.CopilotIsAuthenticated = isAuth;

        if (!isAuth)
        {
            return;

        }

        var credential = await _oauthService.GetCredentialAsync(userId);
        model.CopilotGitHubUsername = credential?.GitHubUsername;
        var models = await _oauthService.ListModelsAsync(userId);
        model.CopilotAvailableModels = models
            .Select(m => new SelectListItem(m.Name, m.Id))
            .ToList();

    }

    private bool IsCopilotConfigured() => _copilotOptions.IsConfigured();
}
