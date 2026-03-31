using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Copilot.Models;
using CrestApps.AI.Chat.Copilot.Services;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Prompting.Services;
using CrestApps.AI.Services;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class ChatInteractionController : Controller
{
    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly ICatalog<ChatInteraction> _interactionCatalog;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<A2AConnection> _a2aConnectionCatalog;
    private readonly ICatalog<McpConnection> _mcpConnectionCatalog;
    private readonly ICatalog<AIDataSource> _dataSourceCatalog;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly FileSystemFileStore _fileStore;
    private readonly IAIDocumentProcessingService _documentProcessingService;
    private readonly MvcAIDocumentIndexingService _documentIndexingService;
    private readonly IInteractionDocumentSettingsProvider _interactionDocumentSettingsProvider;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IAITemplateService _aiTemplateService;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly CopilotOptions _copilotOptions;
    private readonly GitHubOAuthService _oauthService;
    private readonly AIToolDefinitionOptions _toolOptions;

    public ChatInteractionController(
        ICatalogManager<ChatInteraction> interactionManager,
        ICatalog<ChatInteraction> interactionCatalog,
        IChatInteractionPromptStore promptStore,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<A2AConnection> a2aConnectionCatalog,
        ICatalog<McpConnection> mcpConnectionCatalog,
        ICatalog<AIDataSource> dataSourceCatalog,
        IAIProfileManager profileManager,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        FileSystemFileStore fileStore,
        IAIDocumentProcessingService documentProcessingService,
        MvcAIDocumentIndexingService documentIndexingService,
        IInteractionDocumentSettingsProvider interactionDocumentSettingsProvider,
        ISearchIndexProfileStore indexProfileStore,
        IAITemplateService aiTemplateService,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<CopilotOptions> copilotOptions,
        GitHubOAuthService oauthService,
        IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _interactionManager = interactionManager;
        _interactionCatalog = interactionCatalog;
        _promptStore = promptStore;
        _deploymentCatalog = deploymentCatalog;
        _a2aConnectionCatalog = a2aConnectionCatalog;
        _mcpConnectionCatalog = mcpConnectionCatalog;
        _dataSourceCatalog = dataSourceCatalog;
        _profileManager = profileManager;
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _fileStore = fileStore;
        _documentProcessingService = documentProcessingService;
        _documentIndexingService = documentIndexingService;
        _interactionDocumentSettingsProvider = interactionDocumentSettingsProvider;
        _indexProfileStore = indexProfileStore;
        _aiTemplateService = aiTemplateService;
        _orchestratorOptions = orchestratorOptions.Value;
        _copilotOptions = copilotOptions.Value;
        _oauthService = oauthService;
        _toolOptions = toolOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var interactions = await _interactionManager.GetAllAsync();

        return View(interactions.OrderByDescending(i => i.CreatedUtc).ToList());
    }

    public async Task<IActionResult> Create()
    {
        var interaction = await _interactionManager.NewAsync();
        interaction.Title = "Untitled Chat";
        interaction.OwnerId = User.Identity?.Name ?? "anonymous";
        interaction.Author = User.Identity?.Name ?? "anonymous";
        interaction.CreatedUtc = DateTime.UtcNow;

        await _interactionManager.CreateAsync(interaction);

        return RedirectToAction(nameof(Chat), new { id = interaction.ItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChatInteractionViewModel model, List<IFormFile> Documents)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        var interaction = await _interactionManager.NewAsync();

        interaction.Title = model.Title;
        interaction.OwnerId = User.Identity?.Name ?? "anonymous";
        interaction.Author = User.Identity?.Name ?? "anonymous";
        interaction.ChatDeploymentName = model.ChatDeploymentName;
        interaction.OrchestratorName = model.OrchestratorName;
        interaction.SystemMessage = model.SystemMessage;
        interaction.Temperature = model.Temperature;
        interaction.TopP = model.TopP;
        interaction.FrequencyPenalty = model.FrequencyPenalty;
        interaction.PresencePenalty = model.PresencePenalty;
        interaction.MaxTokens = model.MaxTokens;
        interaction.PastMessagesCount = model.PastMessagesCount;
        interaction.DocumentTopN = model.DocumentTopN;
        interaction.A2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        interaction.McpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        interaction.ToolNames = GetValidToolNames(model.SelectedToolNames);
        interaction.AgentNames = await GetValidAgentNamesAsync(model.SelectedAgentNames);
        interaction.CreatedUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(model.DataSourceId))
        {
            var dataSource = await _dataSourceCatalog.FindByIdAsync(model.DataSourceId);

            if (dataSource != null)
            {
                interaction.Put(new DataSourceMetadata { DataSourceId = dataSource.ItemId });
            }
        }

        if (string.Equals(model.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            interaction.Put(new CopilotSessionMetadata
            {
                CopilotModel = model.CopilotModel,
                IsAllowAll = model.CopilotIsAllowAll,
            });
        }

        await _interactionManager.CreateAsync(interaction);

        if (Documents != null && Documents.Count > 0)
        {
            await UploadDocumentsAsync(interaction, Documents);
        }

        return RedirectToAction(nameof(Chat), new { id = interaction.ItemId });
    }

    public async Task<IActionResult> Chat(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var interaction = await _interactionManager.FindByIdAsync(id);

        if (interaction == null)
        {
            return NotFound();
        }

        var prompts = await _promptStore.GetPromptsAsync(id);

        var dataSourceMetadata = interaction.TryGet<DataSourceMetadata>(out var dsm) ? dsm : null;

        var model = new ChatInteractionChatViewModel
        {
            ItemId = interaction.ItemId,
            Title = interaction.Title,
            ChatDeploymentName = interaction.ChatDeploymentName,
            OrchestratorName = interaction.OrchestratorName,
            SystemMessage = interaction.SystemMessage,
            Temperature = interaction.Temperature,
            TopP = interaction.TopP,
            FrequencyPenalty = interaction.FrequencyPenalty,
            PresencePenalty = interaction.PresencePenalty,
            MaxTokens = interaction.MaxTokens,
            PastMessagesCount = interaction.PastMessagesCount,
            DocumentTopN = interaction.DocumentTopN,
            Documents = interaction.Documents ?? [],
            DataSourceId = dataSourceMetadata?.DataSourceId,
            SelectedA2AConnectionIds = interaction.A2AConnectionIds?.ToArray() ?? [],
            SelectedMcpConnectionIds = interaction.McpConnectionIds?.ToArray() ?? [],
            SelectedToolNames = interaction.ToolNames?.ToArray() ?? [],
            SelectedAgentNames = interaction.AgentNames?.ToArray() ?? [],
            ExistingMessages = prompts
                .Where(p => p.Role.Value is "user" or "assistant")
                .Select(m => new { role = m.Role.Value, content = m.Text, id = m.ItemId })
                .ToArray(),
        };

        await PopulateChatDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var interaction = await _interactionManager.FindByIdAsync(id);

        if (interaction == null)
        {
            return NotFound();
        }

        await _promptStore.DeleteAllPromptsAsync(id);
        await _interactionManager.DeleteAsync(interaction);
        await _promptStore.SaveChangesAsync();
        await _interactionCatalog.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(ChatInteractionViewModel model)
    {
        var deployments = await _deploymentCatalog.GetAllAsync();
        model.Deployments = deployments
            .Where(d => d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(
                string.Equals(d.Name, d.ModelName, StringComparison.OrdinalIgnoreCase)
                    ? d.Name
                    : $"{d.Name} ({d.ModelName})",
                d.Name))
            .ToList();

        // Orchestrators
        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

        // Copilot
        model.CopilotAuthenticationType = (int)_copilotOptions.AuthenticationType;
        model.CopilotIsConfigured = IsCopilotConfigured();
        await PopulateCopilotStatusAsync(model);

        // A2A Connections
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

        // MCP Connections
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

        // AI Tools
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

        // AI Agents
        var agentProfiles = await _profileManager.GetAsync(AIProfileType.Agent);
        var selectedAgentNames = new HashSet<string>(model.SelectedAgentNames ?? [], StringComparer.OrdinalIgnoreCase);
        model.AvailableAgents = agentProfiles
            .OrderBy(p => p.DisplayText ?? p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new AgentSelectionItem
            {
                Name = p.Name,
                DisplayText = p.DisplayText ?? p.Name,
                IsSelected = selectedAgentNames.Contains(p.Name),
            })
            .ToList();

        // Prompt Templates
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

        // Document settings
        var documentSettings = await _interactionDocumentSettingsProvider.GetAsync();
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

        // Data Sources
        var dataSources = await _dataSourceCatalog.GetAllAsync();
        model.DataSources = dataSources
            .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId))
            .ToList();
    }

    private async Task PopulateChatDropdownsAsync(ChatInteractionChatViewModel model)
    {
        var deployments = await _deploymentCatalog.GetAllAsync();
        model.Deployments = deployments
            .Where(d => d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(
                string.Equals(d.Name, d.ModelName, StringComparison.OrdinalIgnoreCase)
                    ? d.Name
                    : $"{d.Name} ({d.ModelName})",
                d.Name))
            .ToList();

        // Orchestrators
        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

        // Copilot
        model.CopilotAuthenticationType = (int)_copilotOptions.AuthenticationType;
        model.CopilotIsConfigured = IsCopilotConfigured();
        await PopulateCopilotChatStatusAsync(model);

        // A2A Connections
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

        // MCP Connections
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

        // AI Tools
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

        // AI Agents
        var agentProfiles = await _profileManager.GetAsync(AIProfileType.Agent);
        var selectedAgentNames = new HashSet<string>(model.SelectedAgentNames ?? [], StringComparer.OrdinalIgnoreCase);
        model.AvailableAgents = agentProfiles
            .OrderBy(p => p.DisplayText ?? p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new AgentSelectionItem
            {
                Name = p.Name,
                DisplayText = p.DisplayText ?? p.Name,
                IsSelected = selectedAgentNames.Contains(p.Name),
            })
            .ToList();

        // Prompt Templates
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

        // Document settings
        var documentSettings = await _interactionDocumentSettingsProvider.GetAsync();
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

        // Data Sources
        var dataSources = await _dataSourceCatalog.GetAllAsync();
        model.DataSources = dataSources
            .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId))
            .ToList();
    }

    private async Task<List<string>> GetValidA2AConnectionIdsAsync(IEnumerable<string> selectedIds)
    {
        var allIds = (await _a2aConnectionCatalog.GetAllAsync())
            .Select(connection => connection.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        return (selectedIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && allIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<string>> GetValidMcpConnectionIdsAsync(IEnumerable<string> selectedIds)
    {
        var allIds = (await _mcpConnectionCatalog.GetAllAsync())
            .Select(c => c.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        return (selectedIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && allIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private List<string> GetValidToolNames(IEnumerable<string> selectedNames)
    {
        return (selectedNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name) && _toolOptions.Tools.ContainsKey(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> GetValidAgentNamesAsync(IEnumerable<string> selectedNames)
    {
        var agentProfiles = await _profileManager.GetAsync(AIProfileType.Agent);
        var allNames = agentProfiles
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (selectedNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name) && allNames.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task UploadDocumentsAsync(ChatInteraction interaction, List<IFormFile> files)
    {
        var embeddingGenerator = await _documentProcessingService.CreateEmbeddingGeneratorAsync(null, null);

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            var ext = Path.GetExtension(file.FileName);

            var storagePath = $"documents/{interaction.ItemId}/{UniqueId.GenerateId()}{ext}";
            using (var stream = file.OpenReadStream())
            {
                await _fileStore.SaveFileAsync(storagePath, stream);
            }

            var result = await _documentProcessingService.ProcessFileAsync(
                file,
                interaction.ItemId,
                AIReferenceTypes.Document.ChatInteraction,
                embeddingGenerator);

            if (!result.Success)
            {
                continue;
            }

            await _documentStore.CreateAsync(result.Document);

            foreach (var chunk in result.Chunks)
            {
                await _chunkStore.CreateAsync(chunk);
            }

            await _documentIndexingService.IndexAsync(result.Document, result.Chunks);

            interaction.Documents ??= [];
            interaction.Documents.Add(result.DocumentInfo);
        }

        await _interactionManager.UpdateAsync(interaction);
        await _documentStore.SaveChangesAsync();
    }

    private async Task PopulateCopilotStatusAsync(ChatInteractionViewModel model)
    {
        if (_copilotOptions.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
        {
            var userId = User.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                var isAuth = await _oauthService.IsAuthenticatedAsync(userId);
                model.CopilotIsAuthenticated = isAuth;
                if (isAuth)
                {
                    var cred = await _oauthService.GetCredentialAsync(userId);
                    model.CopilotGitHubUsername = cred?.GitHubUsername;
                    var models = await _oauthService.ListModelsAsync(userId);
                    model.CopilotAvailableModels = models
                        .Select(m => new SelectListItem(m.Name, m.Id))
                        .ToList();
                }
            }
        }
    }

    private async Task PopulateCopilotChatStatusAsync(ChatInteractionChatViewModel model)
    {
        if (_copilotOptions.AuthenticationType == CopilotAuthenticationType.GitHubOAuth)
        {
            var userId = User.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                var isAuth = await _oauthService.IsAuthenticatedAsync(userId);
                model.CopilotIsAuthenticated = isAuth;
                if (isAuth)
                {
                    var cred = await _oauthService.GetCredentialAsync(userId);
                    model.CopilotGitHubUsername = cred?.GitHubUsername;
                    var models = await _oauthService.ListModelsAsync(userId);
                    model.CopilotAvailableModels = models
                        .Select(m => new SelectListItem(m.Name, m.Id))
                        .ToList();
                }
            }
        }

        // Load saved Copilot metadata from interaction
        var interaction = await _interactionManager.FindByIdAsync(model.ItemId);
        if (interaction != null && interaction.TryGet<CopilotSessionMetadata>(out var copilotMeta))
        {
            model.CopilotModel = copilotMeta.CopilotModel;
            model.CopilotIsAllowAll = copilotMeta.IsAllowAll;
        }
    }

    private bool IsCopilotConfigured() => _copilotOptions.IsConfigured();
}
