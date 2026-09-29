using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;

/// <summary>
/// Stars and unstars customers in the messaging workspace. The list of favorites is each agent's own, so anyone who
/// can read a conversation may star its customer.
/// </summary>
public sealed class FavoritesController : Controller
{
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingFavoritesService _favoritesService;
    private readonly MessagingWorkspaceBuilder _workspaceBuilder;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;

    public FavoritesController(
        IMessagingConversationStore conversationStore,
        IMessagingFavoritesService favoritesService,
        MessagingWorkspaceBuilder workspaceBuilder,
        IAuthorizationService authorizationService,
        ILogger<FavoritesController> logger)
    {
        _conversationStore = conversationStore;
        _favoritesService = favoritesService;
        _workspaceBuilder = workspaceBuilder;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    [HttpPost]
    [Admin("messaging/conversation/{id}/favorite", "MessagingFavorite")]
    public async Task<IActionResult> Set(string id, bool favorite, string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var conversation = await _conversationStore.FindByIdAsync(id);

        if (conversation is null)
        {
            return NotFound();
        }

        if (!await _workspaceBuilder.AuthorizeAsync(User, conversation, ConversationOperation.View))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        if (agent is null)
        {
            return BadRequest();
        }

        if (await _favoritesService.SetFavoriteAsync(agent, conversation, name, favorite, HttpContext.RequestAborted) &&
            _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Agent {AgentId} {Action} the customer of messaging conversation {ConversationId}.",
                agent.ItemId.SanitizeLogValue(),
                favorite ? "starred" : "unstarred",
                id.SanitizeLogValue());
        }

        return RedirectToAction(nameof(AdminController.Conversation), "Admin", new { id });
    }
}
