using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
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
        _orchestratorOptions = orchestratorOptions.Value;
        _toolOptions = toolOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _profileManager.GetAllAsync();

        return View(profiles);
    }

    public async Task<IActionResult> Create()
    {
        var model = new AIProfileViewModel { Type = AIProfileType.Chat, UseCaching = true, IsListable = true, IsRemovable = true };
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
            .Where(d => d.Type == AIDeploymentType.Chat)
            .Select(d => new SelectListItem(d.Name, d.ItemId)));

        model.UtilityDeployments = [new SelectListItem("— Default Utility Deployment —", "")];
        model.UtilityDeployments.AddRange(allDeployments
            .Where(d => d.Type == AIDeploymentType.Utility || d.Type == AIDeploymentType.Chat)
            .Select(d => new SelectListItem(d.Name, d.ItemId)));

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default Orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

        var templates = await _templateCatalog.GetAllAsync();
        model.Templates = [new SelectListItem("— No Template —", "")];
        model.Templates.AddRange(templates.Select(t => new SelectListItem(t.DisplayText ?? t.Name, t.ItemId)));

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
}
