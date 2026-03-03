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
    private readonly ICatalog<AIProviderConnection> _connectionCatalog;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly IAIDocumentStore _documentStore;
    private readonly FileSystemFileStore _fileStore;
    private readonly AIOptions _aiOptions;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly AIToolDefinitionOptions _toolOptions;

    public AIProfileController(
        IAIProfileManager profileManager,
        ICatalog<AIProviderConnection> connectionCatalog,
        ICatalog<AIDeployment> deploymentCatalog,
        IAIDocumentStore documentStore,
        FileSystemFileStore fileStore,
        IOptions<AIOptions> aiOptions,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IOptions<AIToolDefinitionOptions> toolOptions)
    {
        _profileManager = profileManager;
        _connectionCatalog = connectionCatalog;
        _deploymentCatalog = deploymentCatalog;
        _documentStore = documentStore;
        _fileStore = fileStore;
        _aiOptions = aiOptions.Value;
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

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Provider is required.");
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

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Provider is required.");
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
        model.Sources = [new SelectListItem("— Select Provider —", "")];
        model.Sources.AddRange(_aiOptions.ProfileSources.Select(s =>
            new SelectListItem(s.Value.DisplayName?.Value ?? s.Key, s.Key)));

        var connections = await _connectionCatalog.GetAllAsync();
        model.Connections = [new SelectListItem("— Default Connection —", "")];
        model.Connections.AddRange(connections.Select(c => new SelectListItem(c.DisplayText ?? c.Name, c.Name)));

        var deployments = await _deploymentCatalog.GetAllAsync();
        model.Deployments = [new SelectListItem("— Default Deployment —", "")];
        model.Deployments.AddRange(deployments.Select(d => new SelectListItem(d.Name, d.Name)));

        var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
        model.Orchestrators = [new SelectListItem("— Default Orchestrator —", "")];
        model.Orchestrators.AddRange(orchestrators.Select(o => new SelectListItem(o.Value.Title ?? o.Key, o.Key)));

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
                Text = text,
                FileSize = file.Length,
                UploadedUtc = DateTime.UtcNow,
            };

            await _documentStore.CreateAsync(document);

            profile.AlterSettings<AIProfileDocumentsMetadata>(m =>
            {
                m.Documents ??= [];
                m.Documents.Add(new ChatInteractionDocumentInfo
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
