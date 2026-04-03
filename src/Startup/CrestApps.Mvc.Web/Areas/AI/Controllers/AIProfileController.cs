using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Copilot.Models;
using CrestApps.AI.Copilot.Services;
using CrestApps.AI.DataSources;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Profiles;
using CrestApps.AI.Services;
using CrestApps.AI.Tooling;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.A2A.ViewModels;
using CrestApps.Mvc.Web.Areas.AI.ViewModels;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.ChatInteractions.ViewModels;
using CrestApps.Mvc.Web.Areas.Indexing.Services;
using CrestApps.Mvc.Web.Areas.Mcp.ViewModels;
using CrestApps.Mvc.Web.Services;
using CrestApps.Services;
using CrestApps.Templates.Services;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Areas.AI.Controllers;

[Area("AI")]
[Authorize(Policy = "Admin")]
public sealed class AIProfileController : Controller
{
    private readonly IAIProfileManager _profileManager;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<AIProfileTemplate> _templateCatalog;
    private readonly ICatalog<A2AConnection> _a2aConnectionCatalog;
    private readonly ICatalog<McpConnection> _mcpConnectionCatalog;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly FileSystemFileStore _fileStore;
    private readonly IAIDocumentProcessingService _documentProcessingService;
    private readonly MvcAIDocumentIndexingService _documentIndexingService;
    private readonly IInteractionDocumentSettingsProvider _interactionDocumentSettingsProvider;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly ITemplateService _aiTemplateService;
    private readonly OrchestratorOptions _orchestratorOptions;

    private readonly CopilotOptions _copilotOptions;
    private readonly GitHubOAuthService _oauthService;
    private readonly AIToolDefinitionOptions _toolOptions;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly ILogger<AIProfileController> _logger;

    public AIProfileController(
        IAIProfileManager profileManager,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<AIProfileTemplate> templateCatalog,
        ICatalog<A2AConnection> a2aConnectionCatalog,
        ICatalog<McpConnection> mcpConnectionCatalog,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        FileSystemFileStore fileStore,
        IAIDocumentProcessingService documentProcessingService,
        MvcAIDocumentIndexingService documentIndexingService,
        IInteractionDocumentSettingsProvider interactionDocumentSettingsProvider,
        ISearchIndexProfileStore indexProfileStore,
        ITemplateService aiTemplateService,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<CopilotOptions> copilotOptions,
        GitHubOAuthService oauthService,
        IOptions<AIToolDefinitionOptions> toolOptions,
        IAIDataSourceStore dataSourceStore,
        ILogger<AIProfileController> logger)
    {
        _profileManager = profileManager;
        _deploymentCatalog = deploymentCatalog;
        _templateCatalog = templateCatalog;
        _a2aConnectionCatalog = a2aConnectionCatalog;
        _mcpConnectionCatalog = mcpConnectionCatalog;
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
        _dataSourceStore = dataSourceStore;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _profileManager.GetAllAsync();

        return View(profiles);

    }

    public async Task<IActionResult> Create([FromQuery] string templateId = null)
    {

        var model = new AIProfileViewModel { Type = AIProfileType.Chat, UseCaching = true, IsListable = true, IsRemovable = true };

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var template = await _templateCatalog.FindByIdAsync(templateId);

            if (template != null)
            {
                var profile = new AIProfile { Type = AIProfileType.Chat };

                ApplyTemplateToProfile(profile, template);

                model = AIProfileViewModel.FromProfile(profile);

                await NormalizeDeploymentSelectorsAsync(model);
            }

        }

        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AIProfileViewModel model, List<IFormFile> Documents)
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

        var profile = new AIProfile { Type = AIProfileType.Chat };

        model.SelectedA2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        model.SelectedMcpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        model.ApplyTo(profile);

        await _profileManager.CreateAsync(profile);

        if (Documents != null && Documents.Count > 0)
        {

            await UploadDocumentsAsync(profile, Documents);
        }

        return RedirectToAction(nameof(Index));

    }

    public async Task<IActionResult> Edit(string id)
    {
        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();

        }

        var model = AIProfileViewModel.FromProfile(profile);

        await PopulateAttachedDocumentsAsync(model);
        await NormalizeDeploymentSelectorsAsync(model);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AIProfileViewModel model, List<IFormFile> Documents, string[] RemovedDocumentIds)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAttachedDocumentsAsync(model);
            await PopulateDropdownsAsync(model);

            return View(model);
        }

        var existing = await _profileManager.FindByIdAsync(model.ItemId);

        if (existing == null)
        {
            return NotFound();
        }

        model.SelectedA2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        model.SelectedMcpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);

        model.ApplyTo(existing);

        if (RemovedDocumentIds is { Length: > 0 })
        {
            await RemoveDocumentsAsync(existing, RemovedDocumentIds);
        }

        if (Documents is { Count: > 0 })
        {
            await UploadDocumentsAsync(existing, Documents);
        }

        await _profileManager.UpdateAsync(existing);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)

        {
            return NotFound();

        }

        await _profileManager.DeleteAsync(profile);

        return RedirectToAction(nameof(Index));

    }

    private async Task PopulateDropdownsAsync(AIProfileViewModel model)
    {
        var allDeployments = await _deploymentCatalog.GetAllAsync();

        model.ChatDeployments = new[] { new SelectListItem("— Default Chat Deployment —", "") }
            .Concat(allDeployments
                .Where(d => d.Type.Supports(AIDeploymentType.Chat))
                .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name)))
            .ToList();

        model.UtilityDeployments = new[] { new SelectListItem("— Default Utility Deployment —", "") }
            .Concat(allDeployments
                .Where(d => d.Type.Supports(AIDeploymentType.Utility) || d.Type.Supports(AIDeploymentType.Chat))
                .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name)))
            .ToList();

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = new[] { new SelectListItem("— Default orchestrator —", "") }
            .Concat(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)))
            .ToList();

        // Copilot
        model.CopilotAuthenticationType = (int)_copilotOptions.AuthenticationType;
        model.CopilotIsConfigured = IsCopilotConfigured();

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

        var templates = await _templateCatalog.GetAllAsync();
        model.Templates = new[] { new SelectListItem("— No Template —", "") }
            .Concat(templates.Select(t => new SelectListItem(t.DisplayText ?? t.Name, t.ItemId)))
            .ToList();

        model.AvailableProfileTemplates = new[] { new SelectListItem("— Select a template to apply —", "") }
            .Concat(templates
                .Where(t => string.Equals(t.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase))
                .Select(t => new SelectListItem(t.DisplayText ?? t.Name, t.ItemId)))
            .ToList();

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

        var allDataSources = await _dataSourceStore.GetAllAsync();

        model.DataSources = new[] { new SelectListItem("— No data source —", "") }
            .Concat(allDataSources
                .OrderBy(ds => ds.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(ds => new SelectListItem(ds.DisplayText, ds.ItemId)))
            .ToList();

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

    private async Task UploadDocumentsAsync(AIProfile profile, List<IFormFile> files)
    {
        var embeddingGenerator = await _documentProcessingService.CreateEmbeddingGeneratorAsync(null, null);

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            try
            {
                var ext = Path.GetExtension(file.FileName);
                var storagePath = $"documents/{profile.ItemId}/{UniqueId.GenerateId()}{ext}";

                using (var stream = file.OpenReadStream())
                {
                    await _fileStore.SaveFileAsync(storagePath, stream);
                }

                var result = await _documentProcessingService.ProcessFileAsync(
                    file,
                    profile.ItemId,
                    AIReferenceTypes.Document.Profile,
                    embeddingGenerator);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to process file '{FileName}': {Error}", file.FileName, result.Error);
                    continue;
                }

                await _documentStore.CreateAsync(result.Document);

                foreach (var chunk in result.Chunks)
                {
                    await _chunkStore.CreateAsync(chunk);
                }

                await _documentIndexingService.IndexAsync(result.Document, result.Chunks);

                var documentsMetadata = profile.As<DocumentsMetadata>();
                documentsMetadata.Documents ??= [];
                documentsMetadata.Documents.Add(result.DocumentInfo);
                profile.Put(documentsMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file '{FileName}'.", file.FileName);
            }
        }

        await _profileManager.UpdateAsync(profile);
        await _documentStore.SaveChangesAsync();
    }

    private async Task PopulateAttachedDocumentsAsync(AIProfileViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ItemId))
        {
            return;
        }

        var storedDocuments = await _documentStore.GetDocumentsAsync(model.ItemId, AIReferenceTypes.Document.Profile);
        var documentsById = (model.AttachedDocuments ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.DocumentId))
            .ToDictionary(d => d.DocumentId, StringComparer.OrdinalIgnoreCase);

        foreach (var document in storedDocuments)
        {
            if (string.IsNullOrWhiteSpace(document.ItemId))
            {
                continue;
            }

            documentsById[document.ItemId] = new DocumentItem
            {
                DocumentId = document.ItemId,
                FileName = document.FileName,
                ContentType = document.ContentType,
                FileSize = document.FileSize,
            };
        }

        model.AttachedDocuments = documentsById.Values
            .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task RemoveDocumentsAsync(AIProfile profile, string[] documentIds)
    {
        var documentsMetadata = profile.As<DocumentsMetadata>();

        if (documentsMetadata?.Documents == null || documentsMetadata.Documents.Count == 0)
        {
            return;
        }

        foreach (var documentId in documentIds)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            try
            {
                var docInfo = documentsMetadata.Documents.FirstOrDefault(d =>
                    string.Equals(d.DocumentId, documentId, StringComparison.OrdinalIgnoreCase));

                if (docInfo == null)
                {
                    continue;
                }

                documentsMetadata.Documents.Remove(docInfo);

                var chunks = await _chunkStore.GetChunksByAIDocumentIdAsync(documentId);

                if (chunks.Count > 0)
                {
                    await _documentIndexingService.DeleteChunksAsync(chunks.Select(c => c.ItemId));
                }

                await _chunkStore.DeleteByDocumentIdAsync(documentId);

                var document = await _documentStore.FindByIdAsync(documentId);

                if (document != null)
                {
                    await _documentStore.DeleteAsync(document);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing document '{DocumentId}'.", documentId);
            }
        }

        profile.Put(documentsMetadata);
        await _documentStore.SaveChangesAsync();
    }

    private static void ApplyTemplateToProfile(AIProfile profile, AIProfileTemplate template)
    {
        var metadata = template.As<ProfileTemplateMetadata>();

        if (metadata == null)
        {
            return;
        }

        if (metadata.ProfileType.HasValue)
        {
            profile.Type = metadata.ProfileType.Value;
        }

        if (!string.IsNullOrWhiteSpace(metadata.ChatDeploymentName))
        {
            profile.ChatDeploymentName = metadata.ChatDeploymentName;
        }

        if (!string.IsNullOrWhiteSpace(metadata.UtilityDeploymentName))
        {
            profile.UtilityDeploymentName = metadata.UtilityDeploymentName;
        }

        if (!string.IsNullOrWhiteSpace(metadata.OrchestratorName))
        {
            profile.OrchestratorName = metadata.OrchestratorName;
        }

        if (!string.IsNullOrWhiteSpace(metadata.WelcomeMessage))
        {
            profile.WelcomeMessage = metadata.WelcomeMessage;
        }

        if (!string.IsNullOrWhiteSpace(metadata.PromptTemplate))
        {
            profile.PromptTemplate = metadata.PromptTemplate;
        }

        if (!string.IsNullOrWhiteSpace(metadata.PromptSubject))
        {
            profile.PromptSubject = metadata.PromptSubject;
        }

        if (metadata.TitleType.HasValue)
        {
            profile.TitleType = metadata.TitleType.Value;
        }

        if (metadata.AgentAvailability.HasValue)
        {
            profile.Put(new AgentMetadata

            {
                Availability = metadata.AgentAvailability.Value,
            });
        }

        var profileMetadata = profile.As<AIProfileMetadata>();

        if (!string.IsNullOrWhiteSpace(metadata.SystemMessage))
        {
            profileMetadata.SystemMessage = metadata.SystemMessage;
        }

        if (metadata.Temperature.HasValue)
        {
            profileMetadata.Temperature = metadata.Temperature;
        }

        if (metadata.TopP.HasValue)
        {
            profileMetadata.TopP = metadata.TopP;
        }

        if (metadata.FrequencyPenalty.HasValue)
        {
            profileMetadata.FrequencyPenalty = metadata.FrequencyPenalty;
        }

        if (metadata.PresencePenalty.HasValue)
        {
            profileMetadata.PresencePenalty = metadata.PresencePenalty;
        }

        if (metadata.MaxOutputTokens.HasValue)
        {
            profileMetadata.MaxTokens = metadata.MaxOutputTokens;
        }

        if (metadata.PastMessagesCount.HasValue)
        {
            profileMetadata.PastMessagesCount = metadata.PastMessagesCount;
        }

        profile.Put(profileMetadata);

        if (metadata.ToolNames?.Length > 0)
        {
            profile.Put(new FunctionInvocationMetadata
            {
                Names = metadata.ToolNames,
            });
        }

        if (metadata.A2AConnectionIds?.Length > 0)
        {
            profile.Put(new AIProfileA2AMetadata

            {
                ConnectionIds = metadata.A2AConnectionIds,
            });
        }

        if (template.TryGet<CopilotSessionMetadata>(out var copilotMetadata))
        {
            profile.Put(new CopilotSessionMetadata
            {
                CopilotModel = copilotMetadata.CopilotModel,
                IsAllowAll = copilotMetadata.IsAllowAll,
            });
        }

        else
        {
            profile.Remove<CopilotSessionMetadata>();
        }

        if (!string.IsNullOrWhiteSpace(metadata.Description))

        {
            profile.Description = metadata.Description;
        }
    }

    private async Task NormalizeDeploymentSelectorsAsync(AIProfileViewModel model)
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

    private bool IsCopilotConfigured() => _copilotOptions.IsConfigured();
}
