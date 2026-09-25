using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;

/// <summary>
/// The messaging workspace: the customer list and, beside it, the open customer's conversation with a tab per
/// channel. Every channel is served by the same actions; the conversation's channel decides where a message goes.
/// </summary>
public sealed class AdminController : Controller
{
    private static readonly char[] _recipientSeparators = ['\n', '\r', ',', ';'];

    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingConversationService _conversationService;
    private readonly IMessagingBroadcastManager _broadcastManager;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IMessagingAvailabilityService _availabilityService;
    private readonly MessagingWorkspaceBuilder _workspaceBuilder;
    private readonly MessagingContactSearch _contactSearch;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public AdminController(
        IMessagingConversationStore conversationStore,
        IMessagingConversationService conversationService,
        IMessagingBroadcastManager broadcastManager,
        IMessagingChannelResolver channelResolver,
        IOmnichannelChannelEndpointManager endpointManager,
        IMessagingAvailabilityService availabilityService,
        MessagingWorkspaceBuilder workspaceBuilder,
        MessagingContactSearch contactSearch,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _conversationStore = conversationStore;
        _conversationService = conversationService;
        _broadcastManager = broadcastManager;
        _channelResolver = channelResolver;
        _endpointManager = endpointManager;
        _availabilityService = availabilityService;
        _workspaceBuilder = workspaceBuilder;
        _contactSearch = contactSearch;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("messaging", "MessagingIndex")]
    public async Task<IActionResult> Index(string show, string channel, int page = 1)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        return View("Workspace", new WorkspaceViewModel
        {
            Inbox = await _workspaceBuilder.BuildInboxAsync(User, agent, show, channel, page, null, HttpContext.RequestAborted),
        });
    }

    // Re-renders the customer list alone, so a new message can reorder it live without reloading the conversation
    // the agent is typing in.
    [Admin("messaging/inbox", "MessagingInboxList")]
    public async Task<IActionResult> InboxList(string show, string channel, string selected, int page = 1)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        return PartialView("_InboxList", await _workspaceBuilder.BuildInboxAsync(User, agent, show, channel, page, selected, HttpContext.RequestAborted));
    }

    [Admin("messaging/conversation/{id}", "MessagingConversation")]
    public async Task<IActionResult> Conversation(string id, string show, string channel, int page = 1, long beforeTicks = 0)
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

        // Mark the thread read for the viewing agent, and pick up a routed thread so it is no longer reassigned.
        if (conversation.UnreadCount != 0 || !conversation.IsRead || conversation.AssignedUtc is not null)
        {
            conversation.IsRead = true;
            conversation.UnreadCount = 0;
            conversation.AssignedUtc = null;
            conversation.ReassignmentAttempts = 0;
            await _conversationStore.UpdateAsync(conversation);
        }

        var beforeUtc = beforeTicks > 0 && beforeTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(beforeTicks, DateTimeKind.Utc)
            : (DateTime?)null;

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        return View("Workspace", new WorkspaceViewModel
        {
            Inbox = await _workspaceBuilder.BuildInboxAsync(User, agent, show, channel, page, conversation.GetCustomerKey(), HttpContext.RequestAborted),
            Thread = await _workspaceBuilder.BuildThreadAsync(
                User,
                conversation,
                beforeUtc,
                target => Url.Action(nameof(Conversation), new { id = target.ItemId, show, channel }),
                (tabChannel, address) => Url.Action(nameof(Start), new { channel = tabChannel, address }),
                HttpContext.RequestAborted),
        });
    }

    // Entry point for "message this customer on this channel": open the single existing conversation for that address
    // (on any of our endpoints), or fall through to the composer prefilled with the recipient. The "Send SMS" button
    // beside a phone field and the channel tabs of a customer both land here.
    [Admin("messaging/start", "MessagingStart")]
    public async Task<IActionResult> Start(string channel, string address)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var messagingChannel = _channelResolver.Get(channel);
        var normalized = messagingChannel?.NormalizeAddress(address);

        if (string.IsNullOrEmpty(normalized))
        {
            return RedirectToAction(nameof(New), new { channel = messagingChannel?.Name });
        }

        var existing = await _conversationStore.FindByContactAsync(messagingChannel.Name, normalized);

        return existing is not null
            ? RedirectToAction(nameof(Conversation), new { id = existing.ItemId })
            : RedirectToAction(nameof(New), new { channel = messagingChannel.Name, to = address });
    }

    [Admin("messaging/new", "MessagingNew")]
    public async Task<IActionResult> New(string channel, string to)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var model = new ComposeViewModel
        {
            Recipients = to,
            Channel = _channelResolver.Get(channel)?.Name,
        };

        await PopulateEndpointsAsync(model);
        model.EndpointId = model.Endpoints.FirstOrDefault(item => item.Selected)?.Value;

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(New))]
    [Admin("messaging/new", "MessagingNew")]
    public async Task<IActionResult> NewPost(ComposeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var endpoint = string.IsNullOrEmpty(model.EndpointId) ? null : await _endpointManager.FindByIdAsync(model.EndpointId);
        var channel = _channelResolver.Get(endpoint?.Channel);

        var recipients = ParseRecipients(model.Recipients)
            .Concat(model.ContactAddresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => channel?.NormalizeAddress(address) ?? address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (endpoint is null || channel is null)
        {
            ModelState.AddModelError(nameof(model.EndpointId), S["Select the address to send from."]);
        }
        else
        {
            var invalid = recipients.Where(address => !channel.IsValidAddress(address)).ToArray();

            if (invalid.Length > 0)
            {
                ModelState.AddModelError(nameof(model.Recipients), S["These are not valid {0} addresses: {1}", channel.DisplayName, string.Join(", ", invalid)]);
            }

            if (recipients.Count > 1 && !channel.Capabilities.SupportsBroadcast)
            {
                ModelState.AddModelError(nameof(model.Recipients), S["{0} does not support group messages. Send to one recipient at a time.", channel.DisplayName]);
            }
        }

        if (recipients.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Recipients), S["At least one recipient is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Body))
        {
            ModelState.AddModelError(nameof(model.Body), S["A message is required."]);
        }

        if (recipients.Count > 1 && !await _authorizationService.AuthorizeAsync(User, MessagingPermissions.SendGroupMessages))
        {
            ModelState.AddModelError(nameof(model.Recipients), S["You are not allowed to send to more than one recipient."]);
        }

        if (!ModelState.IsValid)
        {
            await PopulateEndpointsAsync(model, model.EndpointId);

            return View(model);
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        // A single recipient starts a 1:1 conversation; multiple recipients fan out as a broadcast (each gets their
        // own private 1:1 thread).
        if (recipients.Count == 1)
        {
            var result = await _conversationService.SendDirectAsync(channel.Name, endpoint.Value, recipients[0], model.Body.Trim(), agent?.ItemId);

            if (!result.Succeeded)
            {
                await _notifier.WarningAsync(H["The message could not be sent: {0}", result.Error]);
                await PopulateEndpointsAsync(model, model.EndpointId);

                return View(model);
            }

            return RedirectToAction(nameof(Conversation), new { id = result.Message.ConversationId });
        }

        var broadcast = await _broadcastManager.NewAsync();
        broadcast.ItemId = UniqueId.GenerateId();
        broadcast.Name = S["Group message to {0} recipients", recipients.Count].Value;
        broadcast.Channel = channel.Name;
        broadcast.ServiceAddress = endpoint.Value;
        broadcast.Body = model.Body.Trim();
        broadcast.Recipients = recipients;
        broadcast.OwnerAgentId = agent?.ItemId;
        broadcast.Status = MessagingBroadcastStatus.Queued;

        await _broadcastManager.CreateAsync(broadcast);
        await _notifier.SuccessAsync(H["Queued a group message to {0} recipients as individual 1:1 threads.", recipients.Count]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("messaging/search-contacts", "MessagingSearchContacts")]
    public async Task<IActionResult> SearchContacts(string q, string channel)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        return Json(await _contactSearch.SearchAsync(q, channel, HttpContext.RequestAborted));
    }

    // Returns the message bubbles added since a client-supplied high-water mark (UTC ticks), rendered with the same
    // partial the full thread uses, so the open conversation can append new messages live over SignalR (and a light
    // fallback poll) without a page refresh.
    [Admin("messaging/conversation/{id}/messages", "MessagingConversationMessages")]
    public async Task<IActionResult> ThreadMessages(string id, long afterTicks)
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

        var after = afterTicks > 0 && afterTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(afterTicks, DateTimeKind.Utc)
            : DateTime.MinValue;

        var bubbles = await _workspaceBuilder.BuildBubblesAfterAsync(conversation, after, HttpContext.RequestAborted);

        // A message arriving in the open thread should not leave it flagged unread for the viewing agent.
        if (bubbles.Count > 0 && (conversation.UnreadCount != 0 || !conversation.IsRead))
        {
            conversation.IsRead = true;
            conversation.UnreadCount = 0;
            await _conversationStore.UpdateAsync(conversation);
        }

        return PartialView("_MessageBubbles", bubbles);
    }

    [HttpPost]
    [Admin("messaging/conversation/{id}/claim", "MessagingClaim")]
    public async Task<IActionResult> Claim(string id)
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

        if (!await _workspaceBuilder.AuthorizeAsync(User, conversation, ConversationOperation.Claim))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        if (agent is null)
        {
            await _notifier.WarningAsync(H["You must have an agent profile to claim conversations."]);

            return RedirectToAction(nameof(Conversation), new { id });
        }

        var result = await _conversationService.ClaimAsync(id, agent.ItemId, User);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The conversation could not be claimed: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("messaging/availability", "MessagingAvailability")]
    public async Task<IActionResult> SetAvailability(bool available)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.UseMessagingWorkspace))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        if (agent is null)
        {
            return BadRequest();
        }

        var availability = await _availabilityService.SetAvailableAsync(agent, available);

        return Ok(new { available = availability.Available });
    }

    [HttpPost]
    [Admin("messaging/conversation/{id}/send", "MessagingSend")]
    public async Task<IActionResult> Send(string id, string body, string subject)
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

        if (!await _workspaceBuilder.AuthorizeAsync(User, conversation, ConversationOperation.Send))
        {
            return Forbid();
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        var result = await _conversationService.SendAsync(new MessagingSendRequest
        {
            ConversationId = id,
            Body = body,
            Subject = subject,
            ActingAgentId = agent?.ItemId,
            Principal = User,
        });

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The message could not be sent: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("messaging/conversation/{id}/status", "MessagingStatus")]
    public async Task<IActionResult> SetStatus(string id, ConversationStatus status)
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

        var operation = status switch
        {
            ConversationStatus.Snoozed => ConversationOperation.Snooze,
            ConversationStatus.Open => ConversationOperation.View,
            _ => ConversationOperation.Close,
        };

        if (!await _workspaceBuilder.AuthorizeAsync(User, conversation, operation))
        {
            return Forbid();
        }

        var result = await _conversationService.SetStatusAsync(id, status, User);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The conversation could not be updated: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("messaging/conversation/{id}/transfer", "MessagingTransfer")]
    public async Task<IActionResult> Transfer(string id, string targetAgentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.ViewAllConversations))
        {
            return Forbid();
        }

        var result = await _conversationService.AssignAsync(id, targetAgentId, User);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The conversation could not be transferred: {0}", result.Error]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The conversation was transferred."]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    private static List<string> ParseRecipients(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(_recipientSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task PopulateEndpointsAsync(ComposeViewModel model, string selectedEndpointId = null)
    {
        var options = await _workspaceBuilder.BuildEndpointOptionsAsync(model.Channel, selectedEndpointId);

        model.Endpoints = options.Items;
        model.EndpointChannels = options.EndpointChannels;
    }
}
