using CrestApps.AI;
using CrestApps.AI.A2A.Models;
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
    private readonly IAITemplateService _aiTemplateService;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly AIToolDefinitionOptions _toolOptions;

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
        IAITemplateService aiTemplateService,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<AIToolDefinitionOptions> toolOptions)
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
        _toolOptions = toolOptions.Value;
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
        await NormalizeDeploymentSelectorsAsync(model);
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AIProfileViewModel model)
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

        var existing = await _profileManager.FindByIdAsync(model.ItemId);

        if (existing == null)
        {
            return NotFound();
        }

        model.SelectedA2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        model.SelectedMcpConnectionIds = await GetValidMcpConnectionIdsAsync(model.SelectedMcpConnectionIds);
        model.ApplyTo(existing);

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

        model.ChatDeployments = [new SelectListItem("— Default Chat Deployment —", "")];
        model.ChatDeployments.AddRange(allDeployments
            .Where(d => d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name)));

        model.UtilityDeployments = [new SelectListItem("— Default Utility Deployment —", "")];
        model.UtilityDeployments.AddRange(allDeployments
            .Where(d => d.Type.Supports(AIDeploymentType.Utility) || d.Type.Supports(AIDeploymentType.Chat))
            .Select(d => new SelectListItem(BuildDeploymentLabel(d), d.Name)));

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default Orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

        var templates = await _templateCatalog.GetAllAsync();
        model.Templates = [new SelectListItem("— No Template —", "")];
        model.Templates.AddRange(templates.Select(t => new SelectListItem(t.DisplayText ?? t.Name, t.ItemId)));

        model.AvailableProfileTemplates = [new SelectListItem("— Select a template to apply —", "")];
        model.AvailableProfileTemplates.AddRange(templates
            .Where(t => string.Equals(t.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase))
            .Select(t => new SelectListItem(t.DisplayText ?? t.Name, t.ItemId)));

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
                continue;
            }

            await _documentStore.CreateAsync(result.Document);

            foreach (var chunk in result.Chunks)
            {
                await _chunkStore.CreateAsync(chunk);
            }

            await _documentIndexingService.IndexAsync(result.Document, result.Chunks);

            profile.AlterSettings<DocumentsMetadata>(m =>
            {
                m.Documents ??= [];
                m.Documents.Add(result.DocumentInfo);
            });
        }

        await _profileManager.UpdateAsync(profile);
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

        profile.AlterSettings<AIProfileMetadata>(m =>
        {
            if (!string.IsNullOrWhiteSpace(metadata.SystemMessage))
            {
                m.SystemMessage = metadata.SystemMessage;
            }

            if (metadata.Temperature.HasValue)
            {
                m.Temperature = metadata.Temperature;
            }

            if (metadata.TopP.HasValue)
            {
                m.TopP = metadata.TopP;
            }

            if (metadata.FrequencyPenalty.HasValue)
            {
                m.FrequencyPenalty = metadata.FrequencyPenalty;
            }

            if (metadata.PresencePenalty.HasValue)
            {
                m.PresencePenalty = metadata.PresencePenalty;
            }

            if (metadata.MaxOutputTokens.HasValue)
            {
                m.MaxTokens = metadata.MaxOutputTokens;
            }

            if (metadata.PastMessagesCount.HasValue)
            {
                m.PastMessagesCount = metadata.PastMessagesCount;
            }
        });

        if (metadata.ToolNames?.Length > 0)
        {
            profile.WithSettings(new FunctionInvocationMetadata
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
}
