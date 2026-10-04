using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// The messaging workspace page: the customer list on the left and, when one is open, that customer's
/// conversation on the right with a tab per channel.
/// </summary>
public class WorkspaceViewModel
{
    /// <summary>
    /// Gets or sets the customer list.
    /// </summary>
    public InboxViewModel Inbox { get; set; }

    /// <summary>
    /// Gets or sets the open conversation, or <see langword="null"/> when none is selected.
    /// </summary>
    public ThreadViewModel Thread { get; set; }

    /// <summary>
    /// Gets or sets the new-message composer shown in place of a conversation, or <see langword="null"/>.
    /// </summary>
    public ComposeViewModel Compose { get; set; }
}

/// <summary>
/// One messaging channel as the workspace presents it.
/// </summary>
public class ChannelViewModel
{
    /// <summary>
    /// Gets or sets the channel's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the channel's display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the CSS classes of the channel's icon.
    /// </summary>
    public string IconCssClass { get; set; }
}

/// <summary>
/// One channel tab above a customer's conversation. Every enabled channel gets a tab, so switching channel keeps the
/// same view and only changes where messages are read from and sent to.
/// </summary>
public class ChannelTabViewModel : ChannelViewModel
{
    /// <summary>
    /// Gets or sets where the tab leads: the customer's conversation on the channel, or the composer to start one.
    /// Null when the customer cannot be reached on the channel.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the unread messages waiting on the channel for this customer.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tab is the conversation on screen.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the customer already has a conversation on the channel.
    /// </summary>
    public bool HasConversation { get; set; }

    /// <summary>
    /// Gets or sets the tooltip explaining the tab's state.
    /// </summary>
    public string Tooltip { get; set; }
}

public class ComposeViewModel
{
    public string EndpointId { get; set; }

    public string Recipients { get; set; }

    public IList<string> ContactAddresses { get; set; } = [];

    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the channel the composer was opened for, used to preselect an endpoint of that channel.
    /// </summary>
    public string Channel { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }

    /// <summary>
    /// Gets or sets the channel of each endpoint, keyed by endpoint id, so the composer searches contacts on the
    /// channel of the endpoint picked.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, string> EndpointChannels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the contacts already chosen in the To line, rendered as its selected options so they show by name.
    /// </summary>
    [BindNever]
    public IList<ContactSearchResult> SelectedContacts { get; set; } = [];
}

public class ContactSearchResult
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Address { get; set; }

    public string DisplayAddress { get; set; }
}

public class InboxViewModel
{
    public IList<InboxRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the customers the current agent has starred, shown above the list.
    /// </summary>
    public IList<FavoriteViewModel> Favorites { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the list shows the agent's favorites instead of conversations.
    /// </summary>
    public bool ShowFavorites { get; set; }

    /// <summary>
    /// Gets the value of the <c>show</c> route value that keeps the list on the current view.
    /// </summary>
    public string ShowRouteValue => ShowFavorites
        ? "favorites"
        : Filter == MessagingInboxFilter.All ? null : Filter.ToString().ToLowerInvariant();

    public bool HasAgentProfile { get; set; }

    /// <summary>
    /// Gets or sets the viewing agent's profile, so the page can tell a conversation sent to them from one sent to a
    /// colleague.
    /// </summary>
    public string CurrentAgentId { get; set; }

    public bool Available { get; set; }

    public MessagingInboxFilter Filter { get; set; } = MessagingInboxFilter.All;

    /// <summary>
    /// Gets or sets the channel the list is narrowed to, or <see langword="null"/> for every channel.
    /// </summary>
    public string ChannelFilter { get; set; }

    /// <summary>
    /// Gets or sets the enabled channels, for the channel filter.
    /// </summary>
    public IReadOnlyList<ChannelViewModel> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the customer whose conversation is open, so their row is highlighted.
    /// </summary>
    public string SelectedCustomerKey { get; set; }

    public int AllCount { get; set; }

    public int MineCount { get; set; }

    public int UnassignedCount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public bool HasNextPage => PageSize > 0 && Page * PageSize < TotalCount;

    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// One customer in the list: their most recent conversation, with the unread messages and channels of every
/// conversation of theirs on the page folded in.
/// </summary>
/// <summary>
/// A starred customer, as the row of favorites above the customer list shows them.
/// </summary>
public class FavoriteViewModel
{
    public MessagingFavorite Favorite { get; set; }

    public string Name { get; set; }

    public string AddressDisplay { get; set; }

    public string ChannelIconCssClass { get; set; }

    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets the customer's most recent conversation the viewer may open, or <see langword="null"/> when there
    /// is none yet, in which case opening the favorite starts one.
    /// </summary>
    public MessagingConversation Conversation { get; set; }
}

public class InboxRow
{
    public MessagingConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current agent has starred this customer.
    /// </summary>
    public bool IsFavorite { get; set; }

    public IShape Shape { get; set; }

    public string CustomerKey { get; set; }

    public string CustomerName { get; set; }

    public int UnreadCount { get; set; }

    public IReadOnlyList<ChannelViewModel> Channels { get; set; } = [];
}

public class ThreadViewModel
{
    public MessagingConversation Conversation { get; set; }

    /// <summary>
    /// Gets or sets the channel the conversation runs on.
    /// </summary>
    public ChannelViewModel Channel { get; set; }

    /// <summary>
    /// Gets or sets a tab per enabled channel for this customer.
    /// </summary>
    public IReadOnlyList<ChannelTabViewModel> ChannelTabs { get; set; } = [];

    /// <summary>
    /// Gets or sets the key shared by the customer's conversations on every channel.
    /// </summary>
    public string CustomerKey { get; set; }

    /// <summary>
    /// Gets or sets the contact's address as the channel formats it.
    /// </summary>
    public string ContactAddressDisplay { get; set; }

    /// <summary>
    /// Gets or sets our address as the channel formats it.
    /// </summary>
    public string ServiceAddressDisplay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the composer shows a subject line.
    /// </summary>
    public bool SupportsSubject { get; set; }

    /// <summary>
    /// Gets or sets the longest body the channel accepts, when it has a limit.
    /// </summary>
    public int? MaxBodyLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer can star customers: only someone with an agent profile has a
    /// list of favorites.
    /// </summary>
    public bool CanFavorite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer has starred this customer.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the files the conversation's channel can carry, which decides what the composer's drag and drop,
    /// paste and file picker accept.
    /// </summary>
    public MessagingAttachmentCapabilities Attachments { get; set; } = MessagingAttachmentCapabilities.None;

    public IReadOnlyList<OmnichannelMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the conversation's history entries (its transfers) that belong with the page of messages shown.
    /// </summary>
    public IReadOnlyList<MessagingConversationEvent> Events { get; set; } = [];

    public string Body { get; set; }

    public IReadOnlyList<MessageTemplate> Templates { get; set; } = [];

    public string ContactDisplayText { get; set; }

    public IReadOnlyList<ThreadContact> Contacts { get; set; } = [];

    public IReadOnlyDictionary<string, string> AgentNames { get; set; } = new Dictionary<string, string>();

    public bool CanClaim { get; set; }

    public bool CanChangeStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may hand the conversation to somebody else: they hold it, or
    /// they supervise the workspace.
    /// </summary>
    public bool CanTransfer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversation can also be sent back to a queue's shared pool.
    /// </summary>
    public bool CanTransferToQueue { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who holds the conversation, or <see langword="null"/> when nobody does.
    /// </summary>
    public string HolderName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person viewing the conversation is the one who holds it.
    /// </summary>
    public bool IsHeldByViewer { get; set; }

    /// <summary>
    /// Gets or sets the name of the queue the conversation belongs to, or <see langword="null"/> when it is personal.
    /// </summary>
    public string OwnerQueueName { get; set; }

    public bool CanSend { get; set; }

    public bool IsQuietHours { get; set; }

    public string QuietHoursReason { get; set; }

    public bool CanSendDuringQuietHours { get; set; }

    public bool HasEarlierMessages { get; set; }

    public long EarliestMessageTicks { get; set; }
}

public class ConversationRowViewModel
{
    public MessagingConversation Conversation { get; set; }

    public string ContactName { get; set; }

    public string ContactAddressDisplay { get; set; }

    public string AssignedToName { get; set; }

    public ChannelViewModel Channel { get; set; }
}

public class MessageBubbleViewModel
{
    public OmnichannelMessage Message { get; set; }

    public string ContactLabel { get; set; }

    public string AgentName { get; set; }
}

public class ThreadContact
{
    public string ContentItemId { get; set; }

    public string DisplayName { get; set; }

    public bool IsPrimary { get; set; }
}
