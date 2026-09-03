using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Sms.Workspace.Controllers;

/// <summary>
/// The human SMS portal: the agent's inbox and conversation workspace.
/// </summary>
public sealed class AdminController : Controller
{
    private static readonly char[] _recipientSeparators = ['\n', '\r', ',', ';'];

    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsConversationService _conversationService;
    private readonly ISmsBroadcastManager _broadcastManager;
    private readonly ISmsTemplateManager _templateManager;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ISmsAgentAvailabilityService _availabilityService;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<SmsConversation> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public AdminController(
        ISmsConversationStore conversationStore,
        ISmsConversationService conversationService,
        ISmsBroadcastManager broadcastManager,
        ISmsTemplateManager templateManager,
        IOmnichannelChannelEndpointManager endpointManager,
        IAgentProfileManager agentProfileManager,
        ISmsAgentAvailabilityService availabilityService,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IDisplayManager<SmsConversation> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        ISession session,
        IClock clock,
        OmnichannelContentTypeProvider contentTypeProvider,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _conversationStore = conversationStore;
        _conversationService = conversationService;
        _broadcastManager = broadcastManager;
        _templateManager = templateManager;
        _endpointManager = endpointManager;
        _agentProfileManager = agentProfileManager;
        _availabilityService = availabilityService;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _session = session;
        _clock = clock;
        _contentTypeProvider = contentTypeProvider;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("sms/portal", "SmsPortalIndex")]
    public async Task<IActionResult> Index(string show)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        // Supervisors (ViewAllConversations) see every conversation; everyone else sees the conversations
        // assigned to them or owned by a queue they belong to.
        var canViewAll = await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.ViewAllConversations);

        var conversations = (canViewAll
            ? await _conversationStore.GetAllAsync()
            : await GetVisibleConversationsAsync())
            .ToList();

        var viewModel = new SmsInboxViewModel();

        // Surface the agent's routed-SMS availability toggle (independent of voice presence). A viewer without an
        // agent profile (for example an admin) simply does not see the toggle.
        var currentAgent = await GetCurrentAgentAsync();

        if (currentAgent is not null)
        {
            viewModel.HasAgentProfile = true;
            viewModel.SmsAvailable = _availabilityService.Get(currentAgent).Available;
        }

        // Filter tabs, mirroring the OrchardCore content list: "mine" is assigned to the current agent,
        // "unassigned" is anything not yet owned by a specific agent (unassigned or pooled), "all" is the default.
        var filter = show?.Trim().ToLowerInvariant() switch
        {
            "mine" => SmsInboxFilter.Mine,
            "unassigned" => SmsInboxFilter.Unassigned,
            _ => SmsInboxFilter.All,
        };

        bool IsMine(SmsConversation c) => currentAgent is not null &&
            c.AssignmentStatus == SmsConversationAssignmentStatus.Assigned &&
            string.Equals(c.AssignedAgentId, currentAgent.ItemId, StringComparison.Ordinal);

        static bool IsUnassigned(SmsConversation c) => c.AssignmentStatus != SmsConversationAssignmentStatus.Assigned;

        viewModel.Filter = filter;
        viewModel.AllCount = conversations.Count;
        viewModel.MineCount = conversations.Count(IsMine);
        viewModel.UnassignedCount = conversations.Count(IsUnassigned);

        var filtered = filter switch
        {
            SmsInboxFilter.Mine => conversations.Where(IsMine),
            SmsInboxFilter.Unassigned => conversations.Where(IsUnassigned),
            _ => conversations,
        };

        foreach (var conversation in filtered.OrderByDescending(c => c.LastMessageUtc))
        {
            viewModel.Rows.Add(new SmsInboxRow
            {
                Conversation = conversation,
                Shape = await _displayManager.BuildDisplayAsync(conversation, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        if (canViewAll)
        {
            ViewData["Subtitle"] = S["Every conversation across the tenant."].Value;
        }

        return View(viewModel);
    }

    // Entry point for the "SMS" icon shown next to a phone number: open the single existing conversation for
    // that number (on any of our numbers), or fall through to the composer prefilled with the recipient.
    [Admin("sms/portal/start", "SmsPortalStart")]
    public async Task<IActionResult> Start(string number)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            return RedirectToAction(nameof(New));
        }

        var existing = await _conversationStore.FindByContactAsync(number.GetCleanedPhoneNumber());

        return existing is not null
            ? RedirectToAction(nameof(Conversation), new { id = existing.ItemId })
            : RedirectToAction(nameof(New), new { to = number });
    }

    [Admin("sms/portal/new", "SmsPortalNew")]
    public async Task<IActionResult> New(string to)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        return View(new SmsComposeViewModel
        {
            Recipients = to,
            Endpoints = await BuildEndpointOptionsAsync(),
        });
    }

    [HttpPost]
    [ActionName(nameof(New))]
    [Admin("sms/portal/new", "SmsPortalNew")]
    public async Task<IActionResult> NewPost(SmsComposeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var endpoint = string.IsNullOrEmpty(model.EndpointId) ? null : await _endpointManager.FindByIdAsync(model.EndpointId);
        var recipients = ParseRecipients(model.Recipients)
            .Concat(model.ContactPhones ?? [])
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (endpoint is null)
        {
            ModelState.AddModelError(nameof(model.EndpointId), S["A sending number is required."]);
        }

        if (recipients.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Recipients), S["At least one recipient is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Body))
        {
            ModelState.AddModelError(nameof(model.Body), S["A message is required."]);
        }

        if (recipients.Count > 1 && !await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.SendGroupSms))
        {
            ModelState.AddModelError(nameof(model.Recipients), S["You are not allowed to send to more than one recipient."]);
        }

        if (!ModelState.IsValid)
        {
            model.Endpoints = await BuildEndpointOptionsAsync();

            return View(model);
        }

        var agent = await GetCurrentAgentAsync();

        // A single recipient starts a 1:1 conversation; multiple recipients fan out as a broadcast (each gets
        // their own private 1:1 thread).
        if (recipients.Count == 1)
        {
            var result = await _conversationService.SendDirectAsync(endpoint.Value, recipients[0], model.Body.Trim(), agent?.ItemId);

            if (!result.Succeeded)
            {
                await _notifier.WarningAsync(H["The message could not be sent: {0}", result.Error]);
                model.Endpoints = await BuildEndpointOptionsAsync();

                return View(model);
            }

            return RedirectToAction(nameof(Conversation), new { id = result.Message.ConversationId });
        }

        var broadcast = await _broadcastManager.NewAsync();
        broadcast.ItemId = UniqueId.GenerateId();
        broadcast.Name = S["Group message to {0} recipients", recipients.Count].Value;
        broadcast.FromNumber = endpoint.Value;
        broadcast.Body = model.Body.Trim();
        broadcast.Recipients = recipients;
        broadcast.OwnerAgentId = agent?.ItemId;
        broadcast.Status = SmsBroadcastStatus.Queued;

        await _broadcastManager.CreateAsync(broadcast);
        await _notifier.SuccessAsync(H["Queued a group message to {0} recipients as individual 1:1 threads.", recipients.Count]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("sms/portal/search-contacts", "SmsPortalSearchContacts")]
    public async Task<IActionResult> SearchContacts(string q)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Json(Array.Empty<SmsContactSearchResult>());
        }

        var term = q.Trim();
        var digits = new string(term.Where(char.IsDigit).ToArray());

        // A contact is a content item whose type carries the Omnichannel Contact part. Scope the search to those
        // content types (rather than relying on a contact-index join, which only exists once a contact has been
        // indexed), and match by display name and/or phone number.
        var contactTypes = (await _contentTypeProvider.GetContactContentTypesAsync()).ToArray();

        if (contactTypes.Length == 0)
        {
            return Json(Array.Empty<SmsContactSearchResult>());
        }

        var hits = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);

        // Name matches: the contact's display text (the tenant's title, e.g. first + last name).
        foreach (var item in await _session.Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest && index.ContentType.IsIn(contactTypes) && index.DisplayText.Contains(term))
                .Take(20)
                .ListAsync())
        {
            hits[item.ContentItemId] = item;
        }

        // Phone matches: the indexed cell/home numbers (E.164 and national) contain the typed digits.
        if (digits.Length >= 3)
        {
            foreach (var item in await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(contactTypes))
                    .With<OmnichannelContactIndex>(index =>
                        index.NormalizedPrimaryCellPhoneNumber.Contains(digits) || index.PrimaryCellPhoneNumber.Contains(digits) ||
                        index.NormalizedPrimaryHomePhoneNumber.Contains(digits) || index.PrimaryHomePhoneNumber.Contains(digits))
                    .Take(20)
                    .ListAsync())
            {
                hits[item.ContentItemId] = item;
            }
        }

        var items = hits.Values.Take(20).ToArray();

        var ids = items.Select(item => item.ContentItemId).ToArray();

        var phones = (await _session.QueryIndex<OmnichannelContactIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync())
            .GroupBy(index => index.ContentItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var record = group.FirstOrDefault(r => !string.IsNullOrEmpty(r.NormalizedPrimaryCellPhoneNumber) || !string.IsNullOrEmpty(r.PrimaryCellPhoneNumber) || !string.IsNullOrEmpty(r.NormalizedPrimaryHomePhoneNumber) || !string.IsNullOrEmpty(r.PrimaryHomePhoneNumber)) ?? group.First();
                    return record.NormalizedPrimaryCellPhoneNumber ?? record.PrimaryCellPhoneNumber ?? record.NormalizedPrimaryHomePhoneNumber ?? record.PrimaryHomePhoneNumber;
                },
                StringComparer.OrdinalIgnoreCase);

        var results = items
            .Select(item =>
            {
                // Prefer the indexed (canonical E.164) cell/home number; fall back to any phone method on the
                // contact, so a match is not dropped just because its number is typed something other than
                // "Cell"/"Home" (which is all the index captures).
                var phone = phones.GetValueOrDefault(item.ContentItemId) ?? ResolveContactPhone(item);

                return new SmsContactSearchResult
                {
                    Id = item.ContentItemId,
                    Name = string.IsNullOrEmpty(item.DisplayText) ? phone : item.DisplayText,
                    Phone = phone,
                };
            })
            .Where(result => !string.IsNullOrEmpty(result.Phone))
            .ToArray();

        return Json(results);
    }

    // Reads a usable phone number straight from the contact's ContactMethods bag (preferring a cell number),
    // regardless of how its "Type" is labelled, so search results are not limited to the cell/home numbers the
    // OmnichannelContactIndex captures.
    private static string ResolveContactPhone(ContentItem contact)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) || bag.ContentItems is null)
        {
            return null;
        }

        string firstPhone = null;

        foreach (var method in bag.ContentItems)
        {
            if (!string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !method.TryGet<PhoneNumberInfoPart>(out var phonePart))
            {
                continue;
            }

            var number = phonePart.Number?.PhoneNumber?.Trim();

            if (string.IsNullOrEmpty(number))
            {
                continue;
            }

            firstPhone ??= number;

            if (string.Equals(phonePart.Type?.Text, "Cell", StringComparison.OrdinalIgnoreCase))
            {
                return number;
            }
        }

        return firstPhone;
    }

    [Admin("sms/portal/conversation/{id}", "SmsPortalConversation")]
    public async Task<IActionResult> Conversation(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var conversation = await _conversationStore.FindByIdAsync(id);

        if (conversation is null)
        {
            return NotFound();
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

        var contacts = await ResolveThreadContactsAsync(conversation);
        var titleContact = contacts.FirstOrDefault(contact => contact.IsPrimary) ?? (contacts.Count > 0 ? contacts[0] : null);

        return View(new SmsThreadViewModel
        {
            Conversation = conversation,
            Messages = await GetMessagesAsync(id),
            Templates = (await _templateManager.GetAllAsync()).ToArray(),
            ContactDisplayText = titleContact?.DisplayName,
            Contacts = contacts,
        });
    }

    // Returns the message bubbles added since a client-supplied high-water mark (UTC ticks), rendered with the
    // same partial the full thread uses, so the open conversation can append new messages live over SignalR (and
    // a light fallback poll) without a page refresh.
    [Admin("sms/portal/conversation/{id}/messages", "SmsPortalConversationMessages")]
    public async Task<IActionResult> ThreadMessages(string id, long afterTicks)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var conversation = await _conversationStore.FindByIdAsync(id);

        if (conversation is null)
        {
            return NotFound();
        }

        var after = afterTicks > 0 && afterTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(afterTicks, DateTimeKind.Utc)
            : DateTime.MinValue;

        var messages = (await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == id && index.CreatedUtc > after,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync())
            .ToArray();

        if (messages.Length == 0)
        {
            return PartialView("_MessageBubbles", Array.Empty<SmsMessageBubbleViewModel>());
        }

        // A message arriving in the open thread should not leave it flagged unread for the viewing agent.
        if (conversation.UnreadCount != 0 || !conversation.IsRead)
        {
            conversation.IsRead = true;
            conversation.UnreadCount = 0;
            await _conversationStore.UpdateAsync(conversation);
        }

        var contactLabel = await ResolveContactLabelAsync(conversation);

        var bubbles = messages
            .Select(message => new SmsMessageBubbleViewModel { Message = message, ContactLabel = contactLabel })
            .ToArray();

        return PartialView("_MessageBubbles", bubbles);
    }

    // Resolves the label shown above inbound (customer) bubbles: the linked contact's display name, falling back
    // to the conversation's contact address when no contact is linked or it has no title.
    private async Task<string> ResolveContactLabelAsync(SmsConversation conversation)
    {
        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null && !string.IsNullOrEmpty(contact.DisplayText))
            {
                return contact.DisplayText;
            }
        }

        return conversation.ContactAddress;
    }

    // Lists every contact record that matches the conversation's number, with the linked contact first. A
    // conversation is 1:1, so this is normally a single contact; a shared number surfaces every matching account
    // so the agent can reach the right one (the conversation's own link is picked arbitrarily among matches).
    private async Task<IReadOnlyList<SmsThreadContact>> ResolveThreadContactsAsync(SmsConversation conversation)
    {
        var contentItemIds = new List<string>();

        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            contentItemIds.Add(conversation.ContactContentItemId);
        }

        if (!string.IsNullOrWhiteSpace(conversation.ContactAddress))
        {
            var normalized = conversation.ContactAddress.GetCleanedPhoneNumber();

            var matches = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published &&
                    (index.NormalizedPrimaryCellPhoneNumber == normalized || index.NormalizedPrimaryHomePhoneNumber == normalized))
                .ListAsync();

            foreach (var match in matches)
            {
                if (!contentItemIds.Contains(match.ContentItemId))
                {
                    contentItemIds.Add(match.ContentItemId);
                }
            }
        }

        var contacts = new List<SmsThreadContact>();

        foreach (var contentItemId in contentItemIds)
        {
            var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            if (contact is null)
            {
                continue;
            }

            contacts.Add(new SmsThreadContact
            {
                ContentItemId = contentItemId,
                DisplayName = contact.DisplayText,
                IsPrimary = string.Equals(contentItemId, conversation.ContactContentItemId, StringComparison.Ordinal),
            });
        }

        return contacts;
    }

    [HttpPost]
    [Admin("sms/portal/conversation/{id}/claim", "SmsPortalClaim")]
    public async Task<IActionResult> Claim(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            await _notifier.WarningAsync(H["You must have an agent profile to claim conversations."]);

            return RedirectToAction(nameof(Conversation), new { id });
        }

        var result = await _conversationService.ClaimAsync(id, agent.ItemId);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The conversation could not be claimed: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("sms/portal/availability", "SmsPortalAvailability")]
    public async Task<IActionResult> SetAvailability(bool available)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            return BadRequest();
        }

        var availability = await _availabilityService.SetAvailableAsync(agent, available);

        return Ok(new { available = availability.Available });
    }

    [HttpPost]
    [Admin("sms/portal/conversation/{id}/send", "SmsPortalSend")]
    public async Task<IActionResult> Send(string id, string body)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var agent = await GetCurrentAgentAsync();

        var result = await _conversationService.SendAsync(new SmsSendRequest
        {
            ConversationId = id,
            Body = body,
            ActingAgentId = agent?.ItemId,
        });

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The message could not be sent: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("sms/portal/conversation/{id}/status", "SmsPortalStatus")]
    public async Task<IActionResult> SetStatus(string id, SmsConversationStatus status)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var result = await _conversationService.SetStatusAsync(id, status);

        if (!result.Succeeded)
        {
            await _notifier.WarningAsync(H["The conversation could not be updated: {0}", result.Error]);
        }

        return RedirectToAction(nameof(Conversation), new { id });
    }

    [HttpPost]
    [Admin("sms/portal/conversation/{id}/transfer", "SmsPortalTransfer")]
    public async Task<IActionResult> Transfer(string id, string targetAgentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.ViewAllConversations))
        {
            return Forbid();
        }

        var result = await _conversationService.AssignAsync(id, targetAgentId);

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

    private async Task<IEnumerable<SelectListItem>> BuildEndpointOptionsAsync()
    {
        var endpoints = await _endpointManager.GetAllAsync();

        return endpoints
            .Where(endpoint => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
            .Select(endpoint => new SelectListItem
            {
                Text = string.IsNullOrEmpty(endpoint.DisplayText) ? endpoint.Value : $"{endpoint.DisplayText} ({endpoint.Value})",
                Value = endpoint.ItemId,
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OmnichannelMessage>> GetMessagesAsync(string conversationId)
    {
        var messages = await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            // Insertion order breaks ties so rapid bursts (for example several AI bubbles sent within the same
            // second) keep a stable chronological order.
            .ThenBy(index => index.Id)
            .ListAsync();

        return messages.ToArray();
    }

    private async Task<IReadOnlyCollection<SmsConversation>> GetVisibleConversationsAsync()
    {
        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            return [];
        }

        var conversations = new Dictionary<string, SmsConversation>();

        foreach (var conversation in await _conversationStore.GetForAgentAsync(agent.ItemId))
        {
            conversations[conversation.ItemId] = conversation;
        }

        foreach (var queueId in agent.QueueIds.Concat(agent.AllowedQueueIds).Distinct())
        {
            foreach (var conversation in await _conversationStore.GetForQueueAsync(queueId))
            {
                conversations[conversation.ItemId] = conversation;
            }
        }

        return conversations.Values;
    }

    // Resolves the current user's operator identity, reusing the shared Contact Center agent-profile directory.
    // When the SMS Workspace runs without the full Contact Center Agents/Work Distribution administration (its
    // only hard dependency is the Agent Services directory), there is no entitlements screen to onboard operators,
    // so a bare agent profile is provisioned on first access for any user permitted to use the workspace. When the
    // Contact Center administration is also enabled, that same profile is enriched with queues and entitlements.
    private async Task<AgentProfile> GetCurrentAgentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId);

        if (agent is not null)
        {
            return agent;
        }

        var userName = User.Identity?.Name;

        agent = await _agentProfileManager.NewAsync();
        agent.UserId = userId;
        agent.UserName = userName;
        agent.DisplayName = userName;
        agent.Name = userId;
        agent.CreatedUtc = _clock.UtcNow;

        await _agentProfileManager.CreateAsync(agent);

        return agent;
    }
}
