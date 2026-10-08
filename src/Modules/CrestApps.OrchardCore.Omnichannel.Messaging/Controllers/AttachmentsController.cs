using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;

/// <summary>
/// Shows the pictures and serves the files in a conversation. The link names its conversation, and a file is only
/// served to someone who may read that conversation, so a copied link is no use to anyone else.
/// </summary>
public sealed class AttachmentsController : Controller
{
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingAttachmentStore _attachmentStore;
    private readonly MessagingAttachmentLinks _attachmentLinks;
    private readonly MessagingWorkspaceBuilder _workspaceBuilder;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;

    public AttachmentsController(
        IMessagingConversationStore conversationStore,
        IMessagingAttachmentStore attachmentStore,
        MessagingAttachmentLinks attachmentLinks,
        MessagingWorkspaceBuilder workspaceBuilder,
        IAuthorizationService authorizationService,
        ILogger<AttachmentsController> logger)
    {
        _conversationStore = conversationStore;
        _attachmentStore = attachmentStore;
        _attachmentLinks = attachmentLinks;
        _workspaceBuilder = workspaceBuilder;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    [Admin("messaging/conversation/{id}/attachment/{token}", "MessagingAttachment")]
    public async Task<IActionResult> Show(string id, string token)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var format = _attachmentLinks.TryReadViewToken(id, token, out var attachmentId, out var contentType, out var fileName)
            ? MessagingFileFormats.FindByContentType(contentType)
            : null;

        if (format is null)
        {
            return NotFound();
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

        var bytes = await _attachmentStore.ReadAsync(attachmentId, HttpContext.RequestAborted);

        if (bytes is null)
        {
            _logger.LogWarning("A file in messaging conversation {ConversationId} is no longer stored.", id.SanitizeLogValue());

            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, max-age=86400";

        // Only a picture recognised by its own bytes is shown in the page; anything else is only ever downloaded.
        if (format.IsImage)
        {
            return File(bytes, format.ContentType);
        }

        var downloadName = string.IsNullOrWhiteSpace(fileName)
            ? "attachment" + format.PreferredExtension
            : Path.GetFileName(fileName);

        return File(bytes, format.ContentType, downloadName);
    }
}
