using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.Telephony.Sms.ViewModels;
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
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Telephony.Sms.Controllers;

/// <summary>
/// The human SMS portal: the agent's inbox and conversation workspace.
/// </summary>
[Admin]
public sealed class SmsPortalController : Controller
{
    private static readonly char[] _recipientSeparators = ['\n', '\r', ',', ';'];

    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsConversationService _conversationService;
    private readonly ISmsBroadcastManager _broadcastManager;
    private readonly ISmsTemplateManager _templateManager;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<SmsConversation> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly ISession _session;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public SmsPortalController(
        ISmsConversationStore conversationStore,
        ISmsConversationService conversationService,
        ISmsBroadcastManager broadcastManager,
        ISmsTemplateManager templateManager,
        IOmnichannelChannelEndpointManager endpointManager,
        IAgentProfileManager agentProfileManager,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IDisplayManager<SmsConversation> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        ISession session,
        IHtmlLocalizer<SmsPortalController> htmlLocalizer,
        IStringLocalizer<SmsPortalController> stringLocalizer)
    {
        _conversationStore = conversationStore;
        _conversationService = conversationService;
        _broadcastManager = broadcastManager;
        _templateManager = templateManager;
        _endpointManager = endpointManager;
        _agentProfileManager = agentProfileManager;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _session = session;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("sms/portal", "SmsPortalIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
        {
            return Forbid();
        }

        // Supervisors (ViewAllConversations) see every conversation; everyone else sees the conversations
        // assigned to them or owned by a queue they belong to.
        var canViewAll = await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ViewAllConversations);

        var conversations = canViewAll
            ? await _conversationStore.GetAllAsync()
            : await GetVisibleConversationsAsync();

        var viewModel = new SmsInboxViewModel();

        foreach (var conversation in conversations.OrderByDescending(c => c.LastMessageUtc))
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
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            return RedirectToAction(nameof(New));
        }

        var existing = await _conversationStore.FindByCustomerAsync(number.GetCleanedPhoneNumber());

        return existing is not null
            ? RedirectToAction(nameof(Conversation), new { id = existing.ItemId })
            : RedirectToAction(nameof(New), new { to = number });
    }

    [Admin("sms/portal/new", "SmsPortalNew")]
    public async Task<IActionResult> New(string to)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
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
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
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

        if (recipients.Count > 1 && !await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.SendGroupSms))
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

    [Admin("sms/portal/search-customers", "SmsPortalSearchCustomers")]
    public async Task<IActionResult> SearchCustomers(string q)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Json(Array.Empty<SmsCustomerSearchResult>());
        }

        var term = q.Trim();
        var digits = new string(term.Where(char.IsDigit).ToArray());

        // Restrict to contacts by joining the contact index (a content item is a contact iff it has an
        // OmnichannelContactIndex record), and match by display name and/or phone number.
        var hits = new List<ContentItem>();

        // Name matches: the contact's display text (the tenant's title, e.g. first + last name).
        hits.AddRange(await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.DisplayText.Contains(term))
            .With<OmnichannelContactIndex>()
            .Take(20)
            .ListAsync());

        // Phone matches: normalized (E.164) and national digits both contain the typed digits.
        if (digits.Length >= 3)
        {
            hits.AddRange(await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest)
                .With<OmnichannelContactIndex>(index =>
                    index.NormalizedPrimaryCellPhoneNumber.Contains(digits) || index.PrimaryCellPhoneNumber.Contains(digits) ||
                    index.NormalizedPrimaryHomePhoneNumber.Contains(digits) || index.PrimaryHomePhoneNumber.Contains(digits))
                .Take(20)
                .ListAsync());
        }

        var items = hits
            .GroupBy(item => item.ContentItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(20)
            .ToArray();

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
            .Select(item => new SmsCustomerSearchResult
            {
                Id = item.ContentItemId,
                Name = string.IsNullOrEmpty(item.DisplayText) ? phones.GetValueOrDefault(item.ContentItemId) : item.DisplayText,
                Phone = phones.GetValueOrDefault(item.ContentItemId),
            })
            .Where(result => !string.IsNullOrEmpty(result.Phone))
            .ToArray();

        return Json(results);
    }

    [Admin("sms/portal/conversation/{id}", "SmsPortalConversation")]
    public async Task<IActionResult> Conversation(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
        {
            return Forbid();
        }

        var conversation = await _conversationStore.FindByIdAsync(id);

        if (conversation is null)
        {
            return NotFound();
        }

        // Mark the thread read for the viewing agent.
        if (conversation.UnreadCount != 0 || !conversation.IsRead)
        {
            conversation.IsRead = true;
            conversation.UnreadCount = 0;
            await _conversationStore.UpdateAsync(conversation);
        }

        string contactDisplayText = null;

        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);
            contactDisplayText = contact?.DisplayText;
        }

        return View(new SmsThreadViewModel
        {
            Conversation = conversation,
            Messages = await GetMessagesAsync(id),
            Templates = (await _templateManager.GetEnabledAsync()).ToArray(),
            ContactDisplayText = contactDisplayText,
        });
    }

    [HttpPost]
    [Admin("sms/portal/conversation/{id}/claim", "SmsPortalClaim")]
    public async Task<IActionResult> Claim(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
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
    [Admin("sms/portal/conversation/{id}/send", "SmsPortalSend")]
    public async Task<IActionResult> Send(string id, string body)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
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
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.UseSmsPortal))
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
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ViewAllConversations))
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

    private Task<AgentProfile> GetCurrentAgentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId)
            ? Task.FromResult<AgentProfile>(null)
            : _agentProfileManager.FindByUserIdAsync(userId);
    }
}
