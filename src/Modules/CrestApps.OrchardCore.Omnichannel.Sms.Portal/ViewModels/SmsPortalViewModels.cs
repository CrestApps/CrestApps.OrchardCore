using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.ViewModels;

/// <summary>
/// The view model for starting a new conversation from the portal.
/// </summary>
public class SmsComposeViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the sending SMS channel endpoint (DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the recipient numbers (E.164), one per line or comma-separated. A single recipient starts a
    /// 1:1 conversation; multiple recipients start a broadcast (individual 1:1 threads).
    /// </summary>
    public string Recipients { get; set; }

    /// <summary>
    /// Gets or sets the phone numbers selected through the contact picker.
    /// </summary>
    public IList<string> ContactPhones { get; set; } = [];

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the selectable SMS channel endpoints (DIDs).
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }
}

/// <summary>
/// A contact search result for the compose picker.
/// </summary>
public class SmsContactSearchResult
{
    /// <summary>
    /// Gets or sets the contact content item id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the contact display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the contact's primary phone number (E.164).
    /// </summary>
    public string Phone { get; set; }
}

/// <summary>
/// The inbox list view model: the conversations the current agent can see, each rendered through the display
/// manager so drivers can extend the row.
/// </summary>
public class SmsInboxViewModel
{
    /// <summary>
    /// Gets or sets the rendered conversation rows.
    /// </summary>
    public IList<SmsInboxRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the current user has an agent profile, so the SMS-availability
    /// toggle is only shown to agents who can receive routed assignments.
    /// </summary>
    public bool HasAgentProfile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current agent is accepting routed (push) SMS assignments.
    /// </summary>
    public bool SmsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the active inbox filter.
    /// </summary>
    public SmsInboxFilter Filter { get; set; } = SmsInboxFilter.All;

    /// <summary>
    /// Gets or sets the number of conversations visible to the current user, across all filters.
    /// </summary>
    public int AllCount { get; set; }

    /// <summary>
    /// Gets or sets the number of conversations assigned to the current agent.
    /// </summary>
    public int MineCount { get; set; }

    /// <summary>
    /// Gets or sets the number of conversations not yet assigned to a specific agent.
    /// </summary>
    public int UnassignedCount { get; set; }

    /// <summary>
    /// Gets or sets the one-based page being shown.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many conversations one page holds.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the number of conversations the active filter matches, across every page.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether a page follows this one.
    /// </summary>
    public bool HasNextPage => PageSize > 0 && Page * PageSize < TotalCount;

    /// <summary>
    /// Gets a value indicating whether a page precedes this one.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// A single inbox row: the conversation and its rendered summary shape.
/// </summary>
public class SmsInboxRow
{
    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the rendered summary shape.
    /// </summary>
    public IShape Shape { get; set; }
}

/// <summary>
/// The conversation thread view model: the conversation, its ordered message bubbles, and the composer state.
/// </summary>
public class SmsThreadViewModel
{
    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the ordered messages in the thread.
    /// </summary>
    public IReadOnlyList<OmnichannelMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the composer body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the enabled canned-response templates offered in the composer.
    /// </summary>
    public IReadOnlyList<SmsTemplate> Templates { get; set; } = [];

    /// <summary>
    /// Gets or sets the display name of the linked contact, when the conversation resolves to a contact.
    /// </summary>
    public string ContactDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the contact records that match the conversation's number. A conversation is 1:1, so this is
    /// normally a single contact; it holds more than one only when several CRM records share the same number.
    /// </summary>
    public IReadOnlyList<SmsThreadContact> Contacts { get; set; } = [];

    /// <summary>
    /// Gets or sets the resolved display names of the agents who sent messages in this thread, keyed by agent id.
    /// Populated once so each outbound (human) bubble can show the sending agent's full name instead of an id.
    /// </summary>
    public IReadOnlyDictionary<string, string> AgentNames { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may take ownership of the thread. The Claim button is
    /// hidden when it is false, so the surface matches what the server allows.
    /// </summary>
    public bool CanClaim { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may close, reopen, or mark the thread as spam.
    /// </summary>
    public bool CanChangeStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may send on the thread.
    /// </summary>
    public bool CanSend { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether sending right now would reach the contact outside the hours their
    /// queue keeps, in the contact's local time. The composer warns; it does not block, because a person who
    /// genuinely needs to reach a customer out of hours exists.
    /// </summary>
    public bool IsQuietHours { get; set; }

    /// <summary>
    /// Gets or sets the sentence explaining why it is quiet hours, shown next to the composer.
    /// </summary>
    public string QuietHoursReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer holds the permission to send anyway.
    /// </summary>
    public bool CanSendDuringQuietHours { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the thread has bubbles older than the page being shown, so the
    /// view can offer to load them instead of a long thread loading its whole history on every open.
    /// </summary>
    public bool HasEarlierMessages { get; set; }

    /// <summary>
    /// Gets or sets the UTC ticks of the oldest bubble on this page, which is the cursor the "load earlier"
    /// link reads back from.
    /// </summary>
    public long EarliestMessageTicks { get; set; }
}

/// <summary>
/// The inbox row model rendered by the conversation summary driver: the conversation plus the display names it
/// needs (the contact and the assigned agent), resolved once by the driver so the row can show them as badges.
/// </summary>
public class SmsConversationRowViewModel
{
    /// <summary>
    /// Gets or sets the conversation.
    /// </summary>
    public SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the contact's display name, or <see langword="null"/> when the conversation is not linked to
    /// a named contact (the row then falls back to the contact address).
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the agent the conversation is assigned to, when it is assigned to a
    /// specific agent.
    /// </summary>
    public string AssignedToName { get; set; }
}

/// <summary>
/// The data a single message bubble needs to render. Shared by the full thread render and the live delta partial
/// so the bubble markup lives in one place.
/// </summary>
public class SmsMessageBubbleViewModel
{
    /// <summary>
    /// Gets or sets the message rendered by the bubble.
    /// </summary>
    public OmnichannelMessage Message { get; set; }

    /// <summary>
    /// Gets or sets the label shown on an inbound (customer) bubble — the contact's display name, or the
    /// contact address when no name is known.
    /// </summary>
    public string ContactLabel { get; set; }

    /// <summary>
    /// Gets or sets the display name of the human agent who sent an outbound message, when one did. Left
    /// <see langword="null"/> for customer and automated (AI) messages.
    /// </summary>
    public string AgentName { get; set; }
}

/// <summary>
/// A contact record shown in the conversation sidebar, linking to the contact's account.
/// </summary>
public class SmsThreadContact
{
    /// <summary>
    /// Gets or sets the contact content item id, used to link to the contact's account.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the contact display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the contact the conversation is linked to.
    /// </summary>
    public bool IsPrimary { get; set; }
}
