using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
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
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly FileSystemFileStore _fileStore;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly AIToolDefinitionOptions _toolOptions;

    public AIProfileController(
        IAIProfileManager profileManager,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<AIProfileTemplate> templateCatalog,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        FileSystemFileStore fileStore,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _profileManager = profileManager;
        _deploymentCatalog = deploymentCatalog;
        _templateCatalog = templateCatalog;
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _fileStore = fileStore;
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
    }

    private async Task UploadDocumentsAsync(AIProfile profile, List<IFormFile> files)
    {
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".pdf", ".docx", ".xlsx", ".pptx",
        };

        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html",
        };

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            var ext = Path.GetExtension(file.FileName);

            if (!allowedExtensions.Contains(ext))
            {
                continue;
            }

            var text = string.Empty;

            if (textExtensions.Contains(ext))
            {
                using var reader = new StreamReader(file.OpenReadStream());
                text = await reader.ReadToEndAsync();
            }

            var storagePath = $"documents/{profile.ItemId}/{UniqueId.GenerateId()}{ext}";
            using (var stream = file.OpenReadStream())
            {
                await _fileStore.SaveFileAsync(storagePath, stream);
            }

            var document = new AIDocument
            {
                ItemId = UniqueId.GenerateId(),
                ReferenceId = profile.ItemId,
                ReferenceType = "profile",
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedUtc = DateTime.UtcNow,
            };

            await _documentStore.CreateAsync(document);

            if (!string.IsNullOrEmpty(text))
            {
                await _chunkStore.CreateAsync(new AIDocumentChunk
                {
                    ItemId = UniqueId.GenerateId(),
                    AIDocumentId = document.ItemId,
                    ReferenceId = profile.ItemId,
                    ReferenceType = "profile",
                    Content = text,
                    Index = 0,
                });
            }

            profile.AlterSettings<DocumentsMetadata>(m =>
            {
                m.Documents ??= [];
                m.Documents.Add(new ChatDocumentInfo
                {
                    DocumentId = document.ItemId,
                    FileName = document.FileName,
                    ContentType = document.ContentType,
                    FileSize = document.FileSize,
                });
            });
        }

        await _profileManager.UpdateAsync(profile);
        await _documentStore.SaveChangesAsync();
    }
}
