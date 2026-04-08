using CrestApps.Core.AI;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Mvc.Web.Areas.A2A.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.ViewModels;
using CrestApps.Core.Mvc.Web.Areas.Indexing.Services;
using CrestApps.Core.Mvc.Web.Areas.Mcp.ViewModels;
using CrestApps.Core.Mvc.Web.Services;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Controllers;

[Area("ChatInteractions")]
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
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AppDataSettingsService<ChatInteractionSettings> _chatInteractionSettingsService;
    private readonly AppDataSettingsService<DefaultAIDeploymentSettings> _defaultDeploymentSettingsService;
    private readonly MvcAIDocumentIndexingService _documentIndexingService;
    private readonly InteractionDocumentOptions _interactionDocumentOptions;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly ITemplateService _aiTemplateService;
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
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        AppDataSettingsService<ChatInteractionSettings> chatInteractionSettingsService,
        AppDataSettingsService<DefaultAIDeploymentSettings> defaultDeploymentSettingsService,
        MvcAIDocumentIndexingService documentIndexingService,
        IOptions<InteractionDocumentOptions> interactionDocumentOptions,
        ISearchIndexProfileStore indexProfileStore,
        ITemplateService aiTemplateService,
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
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _chatInteractionSettingsService = chatInteractionSettingsService;
        _defaultDeploymentSettingsService = defaultDeploymentSettingsService;
        _documentIndexingService = documentIndexingService;
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
        interaction.A2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        interaction.McpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        interaction.ToolNames = GetValidToolNames(model.SelectedToolNames);
        interaction.AgentNames = await GetValidAgentNamesAsync(model.SelectedAgentNames);
        interaction.CreatedUtc = DateTime.UtcNow;
        await ApplyMetadataAsync(interaction, model);

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
        var chatInteractionSettings = await _chatInteractionSettingsService.GetAsync();
        var deploymentDefaults = await _defaultDeploymentSettingsService.GetAsync();

        var dataSourceMetadata = interaction.As<DataSourceMetadata>();
        interaction.TryGet<AIDataSourceRagMetadata>(out var ragMetadata);
        var promptMetadata = interaction.As<PromptTemplateMetadata>();

        var chatMode = chatInteractionSettings.ChatMode;
        var hasSpeechToText = !string.IsNullOrWhiteSpace(deploymentDefaults.DefaultSpeechToTextDeploymentName);
        var hasTextToSpeech = !string.IsNullOrWhiteSpace(deploymentDefaults.DefaultTextToSpeechDeploymentName);
        var effectiveChatMode = chatMode switch
        {
            ChatMode.Conversation when hasSpeechToText && hasTextToSpeech => ChatMode.Conversation,
            ChatMode.Conversation when hasSpeechToText => ChatMode.AudioInput,
            ChatMode.AudioInput when hasSpeechToText => ChatMode.AudioInput,
            _ => ChatMode.TextInput,
        };

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
            Documents = interaction.Documents ?? [],
            DataSourceId = string.IsNullOrWhiteSpace(dataSourceMetadata.DataSourceId) ? null : dataSourceMetadata.DataSourceId,
            DataSourceStrictness = ragMetadata?.Strictness,
            DataSourceTopNDocuments = ragMetadata?.TopNDocuments,
            DataSourceIsInScope = ragMetadata?.IsInScope ?? false,
            DataSourceFilter = ragMetadata?.Filter,
            SelectedA2AConnectionIds = interaction.A2AConnectionIds?.ToArray() ?? [],
            SelectedMcpConnectionIds = interaction.McpConnectionIds?.ToArray() ?? [],
            SelectedToolNames = interaction.ToolNames?.ToArray() ?? [],
            SelectedAgentNames = interaction.AgentNames?.ToArray() ?? [],
            PromptTemplates = (promptMetadata.Templates ?? [])
                .Where(template => !string.IsNullOrWhiteSpace(template.TemplateId))
                .Select(template => new PromptTemplateSelectionItem
                {
                    TemplateId = template.TemplateId,
                    PromptParameters = template.Parameters is { Count: > 0 }
                        ? System.Text.Json.JsonSerializer.Serialize(template.Parameters)
                        : null,
                })
                .ToList(),
            ExistingMessages = prompts
                .Where(p => p.Role.Value is "user" or "assistant")
                .Select(m => new { role = m.Role.Value, content = m.Text, id = m.ItemId, references = m.References })
                .ToArray(),
            ChatMode = effectiveChatMode,
            SpeechToTextEnabled = effectiveChatMode is ChatMode.AudioInput or ChatMode.Conversation,
            ConversationModeEnabled = effectiveChatMode == ChatMode.Conversation,
            TextToSpeechEnabled = effectiveChatMode == ChatMode.Conversation,
            TextToSpeechVoiceName = deploymentDefaults.DefaultTextToSpeechVoiceId,
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
        model.Orchestrators = orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)).ToList();

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

        // Data Sources
        var dataSources = await _dataSourceCatalog.GetAllAsync();
        model.DataSources = dataSources
            .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)

            .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId, string.Equals(ds.ItemId, model.DataSourceId, StringComparison.Ordinal)))
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
        model.Orchestrators = orchestrators
            .Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key))
            .ToList();

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

        // Data Sources
        var dataSources = await _dataSourceCatalog.GetAllAsync();
        model.DataSources = dataSources
            .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)

            .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId, string.Equals(ds.ItemId, model.DataSourceId, StringComparison.Ordinal)))
            .ToList();
    }

    private async Task ApplyMetadataAsync(ChatInteraction interaction, ChatInteractionViewModel model)
    {
        var dataSourceId = await ResolveDataSourceIdAsync(model.DataSourceId);

        interaction.Alter<DataSourceMetadata>(metadata =>
        {
            metadata.DataSourceId = dataSourceId;
        });

        interaction.Alter<AIDataSourceRagMetadata>(metadata =>
        {
            metadata.Strictness = model.DataSourceStrictness;
            metadata.TopNDocuments = model.DataSourceTopNDocuments;
            metadata.IsInScope = model.DataSourceIsInScope;
            metadata.Filter = string.IsNullOrWhiteSpace(model.DataSourceFilter) ? null : model.DataSourceFilter;
        });

        interaction.Alter<PromptTemplateMetadata>(metadata =>
        {
            metadata.SetSelections(BuildPromptTemplateSelections(model.PromptTemplates));
        });

        if (string.Equals(model.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            interaction.Alter<CopilotSessionMetadata>(metadata =>
            {
                metadata.CopilotModel = model.CopilotModel;
                metadata.IsAllowAll = model.CopilotIsAllowAll;
            });
        }
        else
        {
            interaction.Remove<CopilotSessionMetadata>();
        }
    }

    private async Task<string> ResolveDataSourceIdAsync(string dataSourceId)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            return null;
        }

        var dataSource = await _dataSourceCatalog.FindByIdAsync(dataSourceId);
        return dataSource?.ItemId;
    }

    private static List<PromptTemplateSelectionEntry> BuildPromptTemplateSelections(IEnumerable<PromptTemplateSelectionItem> promptTemplates)
    {
        return (promptTemplates ?? [])
            .Where(template => !string.IsNullOrWhiteSpace(template.TemplateId))
            .Select(template => new PromptTemplateSelectionEntry
            {
                TemplateId = template.TemplateId,
                Parameters = ParsePromptParameters(template.PromptParameters),
            })
            .ToList();
    }

    private static Dictionary<string, object> ParsePromptParameters(string promptParameters)
    {
        if (string.IsNullOrWhiteSpace(promptParameters))
        {
            return null;
        }

        using var document = System.Text.Json.JsonDocument.Parse(promptParameters);

        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                parameters[property.Name] = property.Value.GetString();
            }
        }

        return parameters.Count > 0 ? parameters : null;
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
        var embeddingGenerator = await CreateEmbeddingGeneratorAsync();

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

    private async Task<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>> CreateEmbeddingGeneratorAsync()
    {
        var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Embedding);
        return deployment == null
            ? null
            : await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);
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
