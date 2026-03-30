using CrestApps.AI.Chat;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Indexes;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class ChatInteractionController : Controller
{
    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly ICatalog<AIDeployment> _deploymentCatalog;
    private readonly ICatalog<A2AConnection> _a2aConnectionCatalog;

    public ChatInteractionController(
        ICatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        ICatalog<AIDeployment> deploymentCatalog,
        ICatalog<A2AConnection> a2aConnectionCatalog)
    {
        _interactionManager = interactionManager;
        _promptStore = promptStore;
        _deploymentCatalog = deploymentCatalog;
        _a2aConnectionCatalog = a2aConnectionCatalog;
    }

    public async Task<IActionResult> Index()
    {
        var interactions = await _interactionManager.GetAllAsync();

        return View(interactions.OrderByDescending(i => i.CreatedUtc).ToList());
    }

    public async Task<IActionResult> Create()
    {
        var model = new ChatInteractionViewModel();
        await PopulateDropdownsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChatInteractionViewModel model)
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
        interaction.ChatDeploymentId = model.ChatDeploymentId;
        interaction.SystemMessage = model.SystemMessage;
        interaction.Temperature = model.Temperature;
        interaction.TopP = model.TopP;
        interaction.FrequencyPenalty = model.FrequencyPenalty;
        interaction.PresencePenalty = model.PresencePenalty;
        interaction.MaxTokens = model.MaxTokens;
        interaction.PastMessagesCount = model.PastMessagesCount;
        interaction.A2AConnectionIds = await GetValidA2AConnectionIdsAsync(model.SelectedA2AConnectionIds);
        interaction.CreatedUtc = DateTime.UtcNow;

        await _interactionManager.CreateAsync(interaction);

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

        ViewData["Prompts"] = prompts;

        return View(interaction);
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
            .Select(d => new SelectListItem(d.Name, d.ItemId))
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
}
