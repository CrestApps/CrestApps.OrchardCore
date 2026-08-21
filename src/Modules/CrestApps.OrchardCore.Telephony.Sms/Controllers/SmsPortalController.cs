using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.Telephony.Sms.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Controllers;

/// <summary>
/// The human SMS portal: the agent's inbox and conversation workspace.
/// </summary>
[Admin]
public sealed class SmsPortalController : Controller
{
    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsConversationService _conversationService;
    private readonly IAgentProfileManager _agentProfileManager;
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
        IAgentProfileManager agentProfileManager,
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
        _agentProfileManager = agentProfileManager;
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

        var conversations = await GetVisibleConversationsAsync();

        var viewModel = new SmsInboxViewModel();

        foreach (var conversation in conversations.OrderByDescending(c => c.LastMessageUtc))
        {
            viewModel.Rows.Add(new SmsInboxRow
            {
                Conversation = conversation,
                Shape = await _displayManager.BuildDisplayAsync(conversation, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(viewModel);
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

        return View(new SmsThreadViewModel
        {
            Conversation = conversation,
            Messages = await GetMessagesAsync(id),
        });
    }

    [Admin("sms/portal/all", "SmsPortalAll")]
    public async Task<IActionResult> All()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TelephonySmsPermissions.ViewAllConversations))
        {
            return Forbid();
        }

        var viewModel = new SmsInboxViewModel();

        foreach (var conversation in (await _conversationStore.GetAllAsync()).OrderByDescending(c => c.LastMessageUtc))
        {
            viewModel.Rows.Add(new SmsInboxRow
            {
                Conversation = conversation,
                Shape = await _displayManager.BuildDisplayAsync(conversation, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(nameof(Index), viewModel);
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
