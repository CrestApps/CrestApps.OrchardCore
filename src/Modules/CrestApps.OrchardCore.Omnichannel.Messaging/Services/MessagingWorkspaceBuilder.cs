using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Assembles what the workspace page shows: the customer list, and the open conversation with a tab per channel.
/// Kept out of the controller so the controller stays about requests and the channel logic stays in one place.
/// </summary>
public sealed class MessagingWorkspaceBuilder
{
    /// <summary>
    /// The most bubbles one page of a thread loads.
    /// </summary>
    public const int ThreadPageSize = 100;

    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IMessageTemplateManager _templateManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;
    private readonly IMessagingAvailabilityService _availabilityService;
    private readonly IMessagingAgentNameProvider _agentNames;
    private readonly IMessagingFavoritesService _favoritesService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly MessagingQuietHoursGuard _quietHoursGuard;
    private readonly IDisplayManager<MessagingConversation> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly MessagingWorkspaceOptions _options;
    private readonly IStringLocalizer S;

    public MessagingWorkspaceBuilder(
        IMessagingConversationStore conversationStore,
        IMessagingChannelResolver channelResolver,
        IOmnichannelChannelEndpointManager endpointManager,
        IMessageTemplateManager templateManager,
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy,
        IMessagingAvailabilityService availabilityService,
        IMessagingAgentNameProvider agentNames,
        IMessagingFavoritesService favoritesService,
        IEnumerable<IActivityQueueManager> queueManagers,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        MessagingQuietHoursGuard quietHoursGuard,
        IDisplayManager<MessagingConversation> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        IClock clock,
        IOptions<MessagingWorkspaceOptions> options,
        IStringLocalizer<MessagingWorkspaceBuilder> stringLocalizer)
    {
        _conversationStore = conversationStore;
        _channelResolver = channelResolver;
        _endpointManager = endpointManager;
        _templateManager = templateManager;
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
        _availabilityService = availabilityService;
        _agentNames = agentNames;
        _favoritesService = favoritesService;
        // Queues are a feature of their own; without it a conversation can only be transferred to a person.
        _queueManager = queueManagers.FirstOrDefault();
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _quietHoursGuard = quietHoursGuard;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _clock = clock;
        _options = options.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the enabled channels as the workspace presents them.
    /// </summary>
    public IReadOnlyList<ChannelViewModel> GetChannels()
        => _channelResolver.GetAll().Select(ToViewModel).ToArray();

    /// <summary>
    /// Resolves the current user's operator identity, reusing the shared Contact Center agent-profile directory.
    /// When the workspace runs without the full Contact Center Agents/Work Distribution administration (its only
    /// hard dependency is the Agent Services directory), there is no entitlements screen to onboard operators, so a
    /// bare agent profile is provisioned on first access for any user permitted to use the workspace. When the
    /// Contact Center administration is also enabled, that same profile is enriched with queues and entitlements.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>The agent profile, or <see langword="null"/> for an anonymous caller.</returns>
    public async Task<AgentProfile> GetCurrentAgentAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId);

        if (agent is not null)
        {
            return agent;
        }

        var userName = user.Identity?.Name;

        agent = await _agentProfileManager.NewAsync();
        agent.UserId = userId;
        agent.UserName = userName;
        agent.DisplayName = userName;
        agent.Name = userId;
        agent.CreatedUtc = _clock.UtcNow;

        await _agentProfileManager.CreateAsync(agent);

        return agent;
    }

    /// <summary>
    /// Builds the customer list.
    /// </summary>
    public async Task<InboxViewModel> BuildInboxAsync(
        ClaimsPrincipal user,
        AgentProfile currentAgent,
        string show,
        string channel,
        int page,
        string selectedCustomerKey,
        CancellationToken cancellationToken)
    {
        // Supervisors (ViewAllConversations) see every conversation; everyone else sees their own conversations and,
        // with ViewQueueConversations, the unclaimed ones of the queues they serve.
        var canViewAll = await _authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewAllConversations);

        var viewModel = new InboxViewModel
        {
            Channels = GetChannels(),
            SelectedCustomerKey = selectedCustomerKey,
        };

        // An unknown channel filter would show an empty list with no way to tell why, so it falls back to all.
        viewModel.ChannelFilter = _channelResolver.Get(channel)?.Name;

        // Surface the agent's messaging availability toggle (independent of voice presence). A viewer without an
        // agent profile (for example an admin) simply does not see the toggle.
        if (currentAgent is not null)
        {
            viewModel.HasAgentProfile = true;
            viewModel.CurrentAgentId = currentAgent.ItemId;
            viewModel.Available = _availabilityService.Get(currentAgent).Available;
        }

        // Filter tabs, mirroring the OrchardCore content list: "mine" is assigned to the current agent,
        // "unassigned" is anything not yet owned by a specific agent (unassigned or pooled), "all" is the default.
        // "favorites" lists the customers the agent starred instead of conversations.
        viewModel.ShowFavorites = string.Equals(show?.Trim(), "favorites", StringComparison.OrdinalIgnoreCase);

        var filter = show?.Trim().ToLowerInvariant() switch
        {
            "mine" => MessagingInboxFilter.Mine,
            "unassigned" => MessagingInboxFilter.Unassigned,
            _ => MessagingInboxFilter.All,
        };

        var visibleQueueIds = await GetVisibleQueueIdsAsync(user, currentAgent);

        MessagingInboxQuery BuildQuery(MessagingInboxFilter tab, int skip, int take) => new()
        {
            AgentId = currentAgent?.ItemId,
            QueueIds = visibleQueueIds,
            IncludeAll = canViewAll,
            Filter = tab,
            Channel = viewModel.ChannelFilter,
            Skip = skip,
            Take = take,
        };

        var pageSize = Math.Max(1, _options.InboxPageSize);
        var currentPage = Math.Max(1, page);

        // Counts and the page are separate indexed reads rather than one unbounded read the caller counts in
        // memory, so an inbox with a hundred thousand threads costs the same as one with fifty.
        viewModel.Filter = filter;
        viewModel.AllCount = await _conversationStore.CountAsync(BuildQuery(MessagingInboxFilter.All, 0, 0), cancellationToken);
        viewModel.MineCount = await _conversationStore.CountAsync(BuildQuery(MessagingInboxFilter.Mine, 0, 0), cancellationToken);
        viewModel.UnassignedCount = await _conversationStore.CountAsync(BuildQuery(MessagingInboxFilter.Unassigned, 0, 0), cancellationToken);

        viewModel.Page = currentPage;
        viewModel.PageSize = pageSize;
        viewModel.TotalCount = filter switch
        {
            MessagingInboxFilter.Mine => viewModel.MineCount,
            MessagingInboxFilter.Unassigned => viewModel.UnassignedCount,
            _ => viewModel.AllCount,
        };

        var channelsByName = viewModel.Channels.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var favorites = _favoritesService.GetFavorites(currentAgent);

        viewModel.Favorites = await BuildFavoritesAsync(favorites, selectedCustomerKey);

        if (viewModel.ShowFavorites)
        {
            // Each starred customer with their latest conversation the agent may open, so the list reads like the
            // inbox; one with none yet opens the composer instead.
            foreach (var favorite in viewModel.Favorites)
            {
                favorite.Conversation = await FindFavoriteConversationAsync(user, favorite.Favorite, cancellationToken);
            }

            viewModel.TotalCount = viewModel.Favorites.Count;

            return viewModel;
        }

        var conversations = await _conversationStore.QueryAsync(
            BuildQuery(filter, (currentPage - 1) * pageSize, pageSize),
            cancellationToken);

        foreach (var group in InboxRowGrouping.Group(conversations))
        {
            viewModel.Rows.Add(new InboxRow
            {
                Conversation = group.Latest,
                IsFavorite = favorites.Any(favorite => favorite.Matches(group.Latest)),
                CustomerKey = group.CustomerKey,
                UnreadCount = group.UnreadCount,
                Channels = group.Channels
                    .Select(name => channelsByName.GetValueOrDefault(name))
                    .Where(item => item is not null)
                    .ToArray(),
                Shape = await _displayManager.BuildDisplayAsync(group.Latest, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return viewModel;
    }

    private async Task<MessagingConversation> FindFavoriteConversationAsync(ClaimsPrincipal user, MessagingFavorite favorite, CancellationToken cancellationToken)
    {
        var candidates = (await _conversationStore.GetForCustomerAsync(favorite.CustomerKey, cancellationToken))?.ToList() ?? [];

        // A customer starred by their number, who has since been linked to a contact, is found by the number.
        if (candidates.Count == 0 && !string.IsNullOrEmpty(favorite.Channel) && !string.IsNullOrEmpty(favorite.ContactAddress))
        {
            var byAddress = await _conversationStore.FindByContactAsync(favorite.Channel, favorite.ContactAddress, cancellationToken);

            if (byAddress is not null)
            {
                candidates.Add(byAddress);
            }
        }

        foreach (var conversation in candidates.OrderByDescending(item => item.LastMessageUtc ?? item.CreatedUtc))
        {
            if (await AuthorizeAsync(user, conversation, ConversationOperation.View))
            {
                return conversation;
            }
        }

        return null;
    }

    // The starred customers, named by their contact record as it reads now, and by the name they were starred under
    // when the record is gone.
    private async Task<IList<FavoriteViewModel>> BuildFavoritesAsync(IReadOnlyList<MessagingFavorite> favorites, string selectedCustomerKey)
    {
        if (favorites.Count == 0)
        {
            return [];
        }

        var contactIds = favorites
            .Select(favorite => favorite.ContactContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var contacts = contactIds.Length == 0
            ? new Dictionary<string, ContentItem>(StringComparer.Ordinal)
            : (await _contentManager.GetAsync(contactIds, VersionOptions.Latest))
                .Where(item => item is not null)
                .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var result = new List<FavoriteViewModel>();

        foreach (var favorite in favorites)
        {
            var channel = _channelResolver.Get(favorite.Channel);
            var addressDisplay = channel?.FormatAddress(favorite.ContactAddress) ?? favorite.ContactAddress;
            var contactName = !string.IsNullOrEmpty(favorite.ContactContentItemId) && contacts.TryGetValue(favorite.ContactContentItemId, out var contact)
                ? contact.DisplayText
                : null;

            result.Add(new FavoriteViewModel
            {
                Favorite = favorite,
                Name = !string.IsNullOrWhiteSpace(contactName)
                    ? contactName
                    : !string.IsNullOrWhiteSpace(favorite.DisplayName) ? favorite.DisplayName : addressDisplay,
                AddressDisplay = addressDisplay,
                ChannelIconCssClass = channel?.IconCssClass,
                IsSelected = !string.IsNullOrEmpty(selectedCustomerKey) &&
                    string.Equals(favorite.CustomerKey, selectedCustomerKey, StringComparison.Ordinal),
            });
        }

        return result;
    }

    /// <summary>
    /// Counts the conversations waiting on the user, for the badge on the Messaging admin menu item: the unread open
    /// conversations assigned to them, and the unread open conversations nobody has taken yet among those the inbox
    /// shows them (the queues they serve, or every queue for a supervisor). It applies the inbox's own visibility
    /// rules, so the badge never counts a conversation the user could not open.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of conversations waiting on the user.</returns>
    public async Task<int> CountNeedingAttentionAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        // Every admin page asks for this number, so the profile is only looked up here. It is provisioned when the
        // user first opens the workspace, not as a side effect of browsing some other page.
        var agent = string.IsNullOrEmpty(userId)
            ? null
            : await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);

        var canViewAll = await _authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewAllConversations);

        if (agent is null && !canViewAll)
        {
            return 0;
        }

        var visibleQueueIds = await GetVisibleQueueIdsAsync(user, agent);

        MessagingInboxQuery BuildQuery(MessagingInboxFilter tab) => new()
        {
            AgentId = agent?.ItemId,
            QueueIds = visibleQueueIds,
            IncludeAll = canViewAll,
            Filter = tab,
            OpenOnly = true,
            UnreadOnly = true,
        };

        // The two tabs never overlap (one is assigned, the other is not), so their counts add up without counting a
        // conversation twice. A supervisor without a profile has nothing assigned to them.
        var mine = agent is null
            ? 0
            : await _conversationStore.CountAsync(BuildQuery(MessagingInboxFilter.Mine), cancellationToken);

        return mine + await _conversationStore.CountAsync(BuildQuery(MessagingInboxFilter.Unassigned), cancellationToken);
    }

    // The queue a conversation belongs to, by name, so the header can say whose shared inbox it is in.
    private async Task<string> GetOwnerQueueNameAsync(MessagingConversation conversation, CancellationToken cancellationToken)
    {
        if (_queueManager is null ||
            conversation.OwnerType != ConversationOwnerType.Queue ||
            string.IsNullOrEmpty(conversation.OwnerId) ||
            ContactCenterConstants.IsDirectRoutingQueue(conversation.OwnerId))
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(conversation.OwnerId, cancellationToken);

        return string.IsNullOrWhiteSpace(queue?.Name) ? null : queue.Name;
    }

    /// <summary>
    /// Builds the open conversation, its channel tabs and its composer.
    /// </summary>
    public async Task<ThreadViewModel> BuildThreadAsync(
        ClaimsPrincipal user,
        MessagingConversation conversation,
        DateTime? beforeUtc,
        Func<MessagingConversation, string> conversationUrl,
        Func<string, string, string> startUrl,
        CancellationToken cancellationToken)
    {
        var channel = _channelResolver.Get(conversation.Channel);
        var contacts = await ResolveThreadContactsAsync(conversation, channel, cancellationToken);
        var titleContact = contacts.FirstOrDefault(contact => contact.IsPrimary) ?? (contacts.Count > 0 ? contacts[0] : null);

        var messages = await GetMessagesAsync(conversation.ItemId, beforeUtc, cancellationToken);

        // A full page came back, so there is at least one more bubble before it worth offering.
        var hasEarlierMessages = messages.Count == ThreadPageSize;
        var canTransfer = await AuthorizeAsync(user, conversation, ConversationOperation.Transfer);

        // Warn, do not block: an agent who genuinely needs to reach a customer out of hours can still send, but they
        // do it knowing what time it is where the customer is. Only channels that observe quiet hours are judged.
        var quietHours = channel?.Capabilities.ObservesQuietHours == true
            ? await _quietHoursGuard.EvaluateAsync(conversation, cancellationToken)
            : QuietHoursDecision.Open;

        var customerKey = conversation.GetCustomerKey();

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var viewer = string.IsNullOrEmpty(userId) ? null : await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);
        var holderId = conversation.GetHolderAgentId();

        return new ThreadViewModel
        {
            Conversation = conversation,
            CanFavorite = viewer is not null,
            IsFavorite = _favoritesService.IsFavorite(viewer, conversation),
            Channel = channel is null ? null : ToViewModel(channel),
            CustomerKey = customerKey,
            ChannelTabs = ChannelTabsBuilder.Build(
                _channelResolver.GetAll(),
                conversation,
                await _conversationStore.GetForCustomerAsync(customerKey, cancellationToken),
                await GetContactAddressesAsync(conversation),
                conversationUrl,
                startUrl,
                S),
            ContactAddressDisplay = channel?.FormatAddress(conversation.ContactAddress) ?? conversation.ContactAddress,
            ServiceAddressDisplay = channel?.FormatAddress(conversation.ServiceAddress) ?? conversation.ServiceAddress,
            SupportsSubject = channel?.Capabilities.SupportsSubject == true,
            MaxBodyLength = channel?.Capabilities.MaxBodyLength,
            Attachments = channel?.Capabilities.Attachments ?? MessagingAttachmentCapabilities.None,
            Messages = messages,
            Events = ThreadTimeline.ForPage(conversation.History, messages, beforeUtc, hasEarlierMessages),
            Templates = (await _templateManager.GetAllAsync(cancellationToken)).ToArray(),
            ContactDisplayText = titleContact?.DisplayName,
            Contacts = contacts,
            AgentNames = await ResolveAgentNamesAsync(messages),
            // A conversation on a channel that is no longer enabled can still be read, but nothing can leave on it.
            CanClaim = await AuthorizeAsync(user, conversation, ConversationOperation.Claim),
            CanChangeStatus = await AuthorizeAsync(user, conversation, ConversationOperation.Close),
            CanTransfer = canTransfer,
            CanTransferToQueue = canTransfer && _queueManager is not null,
            HolderName = holderId is null ? null : await _agentNames.GetDisplayNameAsync(holderId, cancellationToken),
            IsHeldByViewer = holderId is not null && string.Equals(holderId, viewer?.ItemId, StringComparison.OrdinalIgnoreCase),
            OwnerQueueName = await GetOwnerQueueNameAsync(conversation, cancellationToken),
            CanSend = channel is not null && await AuthorizeAsync(user, conversation, ConversationOperation.Send),
            IsQuietHours = quietHours.IsQuietHours,
            QuietHoursReason = quietHours.Reason,
            CanSendDuringQuietHours = await _authorizationService.AuthorizeAsync(user, MessagingPermissions.SendDuringQuietHours),
            HasEarlierMessages = hasEarlierMessages,
            EarliestMessageTicks = messages.Count > 0 ? messages[0].CreatedUtc.Ticks : 0,
        };
    }

    /// <summary>
    /// Applies the per-thread rule on top of the workspace permission: the caller must be allowed to perform this
    /// operation on this specific conversation, not merely to use the workspace.
    /// </summary>
    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, MessagingConversation conversation, ConversationOperation operation)
        => _authorizationService.AuthorizeAsync(
            user,
            MessagingPermissions.UseMessagingWorkspace,
            new ConversationAuthorizationResource(conversation, operation));

    /// <summary>
    /// Builds the bubbles for messages added since a client-supplied high-water mark, so the open conversation can
    /// append new messages live without a page refresh.
    /// </summary>
    public async Task<IReadOnlyList<MessageBubbleViewModel>> BuildBubblesAfterAsync(MessagingConversation conversation, DateTime afterUtc, CancellationToken cancellationToken)
    {
        var conversationId = conversation.ItemId;

        var messages = MessagingThreadDeduplicator.Collapse((await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId && index.CreatedUtc > afterUtc,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .Take(ThreadPageSize)
            .ListAsync(cancellationToken))
            .ToArray());

        if (messages.Count == 0)
        {
            return [];
        }

        var contactLabel = await ResolveContactLabelAsync(conversation);
        var agentNames = await ResolveAgentNamesAsync(messages);

        return messages
            .Select(message => new MessageBubbleViewModel
            {
                Message = message,
                ContactLabel = contactLabel,
                AgentName = !message.IsInbound && !string.IsNullOrEmpty(message.SentByAgentId) && agentNames.TryGetValue(message.SentByAgentId, out var agentName)
                    ? agentName
                    : null,
            })
            .ToArray();
    }

    /// <summary>
    /// Lists every endpoint of every enabled messaging channel, grouped by channel, so one composer serves them all
    /// and the endpoint picked decides the channel. The selected endpoint, else the preferred channel's first
    /// endpoint, else the first endpoint, is preselected.
    /// </summary>
    /// <param name="preferredChannel">The channel whose first endpoint is preselected when none is selected.</param>
    /// <param name="selectedEndpointId">The endpoint already selected, if any.</param>
    /// <returns>The options, and the channel of each endpoint keyed by endpoint id.</returns>
    public async Task<(IReadOnlyList<SelectListItem> Items, IReadOnlyDictionary<string, string> EndpointChannels)> BuildEndpointOptionsAsync(
        string preferredChannel,
        string selectedEndpointId = null)
    {
        var endpoints = await _endpointManager.GetAllAsync();
        var items = new List<SelectListItem>();
        var endpointChannels = new Dictionary<string, string>(StringComparer.Ordinal);
        SelectListItem preferred = null;

        foreach (var channel in _channelResolver.GetAll())
        {
            var group = new SelectListGroup { Name = channel.DisplayName.Value };
            var isPreferred = string.Equals(channel.Name, preferredChannel, StringComparison.OrdinalIgnoreCase);

            foreach (var endpoint in endpoints.Where(endpoint => endpoint.HasCapability(channel.Name) && !endpointChannels.ContainsKey(endpoint.ItemId)))
            {
                var address = channel.FormatAddress(endpoint.Value);

                var item = new SelectListItem
                {
                    Text = string.IsNullOrEmpty(endpoint.DisplayText) ? address : $"{endpoint.DisplayText} ({address})",
                    Value = endpoint.ItemId,
                    Group = group,
                };

                if (isPreferred)
                {
                    preferred ??= item;
                }

                items.Add(item);
                endpointChannels[endpoint.ItemId] = channel.Name;
            }
        }

        // A requested endpoint that is not offered (removed, or no longer used for messaging) falls back like none was.
        var selected = (string.IsNullOrEmpty(selectedEndpointId) ? null : items.FirstOrDefault(item => item.Value == selectedEndpointId))
            ?? preferred
            ?? items.FirstOrDefault();

        if (selected is not null)
        {
            selected.Selected = true;
        }

        return (items, endpointChannels);
    }

    /// <summary>
    /// Lists the recipients who already have a conversation on the channel that the user may not send on. Starting a
    /// message to one of them does not start anything: it posts into that conversation, from its number, and reopens it
    /// when closed, so a colleague's or another queue's customer could be written to from the composer.
    /// </summary>
    /// <param name="user">The user starting the message.</param>
    /// <param name="channel">The channel's technical name.</param>
    /// <param name="contactAddresses">The recipients, normalized for the channel.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The recipients the user may not reach, in the order given.</returns>
    public async Task<IReadOnlyList<string>> FindUnreachableRecipientsAsync(
        ClaimsPrincipal user,
        string channel,
        IEnumerable<string> contactAddresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (await _authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewAllConversations))
        {
            return [];
        }

        var unreachable = new List<string>();

        foreach (var address in contactAddresses ?? [])
        {
            var existing = await _conversationStore.FindByContactAsync(channel, address, cancellationToken);

            if (existing is not null && !await AuthorizeAsync(user, existing, ConversationOperation.Send))
            {
                unreachable.Add(address);
            }
        }

        return unreachable;
    }

    // The queues whose shared conversations the agent sees: the ones they belong to and the ones they may serve, as
    // far as their entitlements allow. Opening a conversation already applies the entitlements; the list, the count
    // and the notifications did not, so an agent whose queue was taken away kept seeing its customers there. A role
    // without the queue permission sees no queue's shared inbox at all, only the conversations that are the agent's own.
    private async Task<string[]> GetVisibleQueueIdsAsync(ClaimsPrincipal user, AgentProfile agent)
    {
        if (agent is null || !await _authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewQueueConversations))
        {
            return [];
        }

        return agent.QueueIds.Concat(agent.AllowedQueueIds)
            .Where(queueId => !string.IsNullOrEmpty(queueId) && _entitlementPolicy.AllowsQueue(agent, queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ChannelViewModel ToViewModel(IMessagingChannel channel)
        => new()
        {
            Name = channel.Name,
            DisplayName = channel.DisplayName.Value,
            IconCssClass = channel.IconCssClass,
        };

    // The customer's addresses on every channel, read from the linked contact, so a channel they have not written on
    // yet still offers to start a conversation there.
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetContactAddressesAsync(MessagingConversation conversation)
    {
        var addresses = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null)
            {
                foreach (var channel in _channelResolver.GetAll())
                {
                    addresses[channel.Name] = channel.GetContactAddresses(contact);
                }
            }
        }

        return addresses;
    }

    // Reads the newest page of a thread, or the page immediately before a cursor when the agent asks for earlier
    // history. A long-running thread would otherwise load every bubble it has ever carried on every open.
    private async Task<IReadOnlyList<OmnichannelMessage>> GetMessagesAsync(string conversationId, DateTime? beforeUtc, CancellationToken cancellationToken)
    {
        var query = beforeUtc.HasValue
            ? _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId && index.CreatedUtc < beforeUtc.Value,
                collection: OmnichannelConstants.CollectionName)
            : _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId,
                collection: OmnichannelConstants.CollectionName);

        // Taken newest-first so the bound reaches the most recent page, then reversed for display.
        var messages = await query
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Take(ThreadPageSize)
            .ListAsync(cancellationToken);

        // Threads written before each message was recorded once may still hold a message twice; show it once.
        return MessagingThreadDeduplicator.Collapse(messages
            .Reverse()
            .ToArray());
    }

    // Resolves the display name of each distinct human agent that sent a message in the thread, keyed by agent id, so a
    // bubble never shows a raw id.
    private async Task<IReadOnlyDictionary<string, string>> ResolveAgentNamesAsync(IEnumerable<OmnichannelMessage> messages)
    {
        var agentIds = messages
            .Where(message => !message.IsInbound && !string.IsNullOrEmpty(message.SentByAgentId))
            .Select(message => message.SentByAgentId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var agentId in agentIds)
        {
            var name = await _agentNames.GetDisplayNameAsync(agentId);

            if (!string.IsNullOrEmpty(name))
            {
                names[agentId] = name;
            }
        }

        return names;
    }

    // Resolves the label shown above inbound (customer) bubbles: the linked contact's display name, falling back to
    // the conversation's contact address when no contact is linked or it has no title.
    private async Task<string> ResolveContactLabelAsync(MessagingConversation conversation)
    {
        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null && !string.IsNullOrEmpty(contact.DisplayText))
            {
                return contact.DisplayText;
            }
        }

        return _channelResolver.Get(conversation.Channel)?.FormatAddress(conversation.ContactAddress) ?? conversation.ContactAddress;
    }

    // Lists every contact record that matches the conversation's address, with the linked contact first. A
    // conversation is 1:1, so this is normally a single contact; a shared address surfaces every matching account so
    // the agent can reach the right one (the conversation's own link is picked arbitrarily among matches).
    private async Task<IReadOnlyList<ThreadContact>> ResolveThreadContactsAsync(
        MessagingConversation conversation,
        IMessagingChannel channel,
        CancellationToken cancellationToken)
    {
        var contentItemIds = new List<string>();

        if (!string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            contentItemIds.Add(conversation.ContactContentItemId);
        }

        if (channel is not null && !string.IsNullOrWhiteSpace(conversation.ContactAddress))
        {
            foreach (var contentItemId in await channel.FindContactIdsAsync(conversation.ContactAddress, cancellationToken))
            {
                if (!contentItemIds.Contains(contentItemId))
                {
                    contentItemIds.Add(contentItemId);
                }
            }
        }

        var contacts = new List<ThreadContact>();

        foreach (var contentItemId in contentItemIds)
        {
            var contact = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            if (contact is null)
            {
                continue;
            }

            contacts.Add(new ThreadContact
            {
                ContentItemId = contentItemId,
                DisplayName = contact.DisplayText,
                IsPrimary = string.Equals(contentItemId, conversation.ContactContentItemId, StringComparison.Ordinal),
            });
        }

        return contacts;
    }
}
