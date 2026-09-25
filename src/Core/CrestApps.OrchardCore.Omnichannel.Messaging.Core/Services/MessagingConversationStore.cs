using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="IMessagingConversationStore"/>.
/// </summary>
public sealed class MessagingConversationStore : DocumentCatalog<MessagingConversation, MessagingConversationIndex>, IMessagingConversationStore
{
    private const int MaxConversationsPerCustomer = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public MessagingConversationStore(ISession session)
        : base(session)
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<MessagingConversation> FindByAddressesAsync(string channel, string serviceAddress, string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => index.Channel == channel && index.ServiceAddress == serviceAddress && index.ContactAddress == contactAddress,
            collection: MessagingStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MessagingConversation> FindByContactAsync(string channel, string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<MessagingConversation, MessagingConversationIndex>(
                index => index.Channel == channel && index.ContactAddress == contactAddress,
                collection: MessagingStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ContainsMessageAsync(string conversationId, string providerMessageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(providerMessageId))
        {
            return false;
        }

        // Scoped to the conversation: the webhook's own audit row carries the same provider id but no thread.
        return await Session.QueryIndex<OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId && index.ProviderMessageId == providerMessageId,
                collection: OmnichannelConstants.CollectionName)
            .CountAsync(cancellationToken) > 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OmnichannelMessage>> GetMessagesAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        return (await Session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessagingConversation>> GetForCustomerAsync(string customerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerKey);

        // A customer holds one thread per channel and endpoint, so this is a handful of rows however long the
        // tenant has been running; the bound only stops a pathological customer from loading without limit.
        var conversations = await Session.Query<MessagingConversation, MessagingConversationIndex>(
                index => index.CustomerKey == customerKey,
                collection: MessagingStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .Take(MaxConversationsPerCustomer)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MessagingConversation>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var personal = ConversationOwnerType.Personal.ToString();

        var conversations = await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => (index.AssignedAgentId == agentId || (index.OwnerType == personal && index.OwnerId == agentId)),
            collection: MessagingStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public Task<int> CountOpenAssignedAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var open = ConversationStatus.Open.ToString();
        var assigned = ConversationAssignmentStatus.Assigned.ToString();

        return Session.QueryIndex<MessagingConversationIndex>(
            index => index.AssignedAgentId == agentId && index.Status == open && index.AssignmentStatus == assigned,
            collection: MessagingStorage.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, int>> CountOpenAssignedAsync(
        IReadOnlyCollection<string> agentIds,
        CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (agentIds is null || agentIds.Count == 0)
        {
            return counts;
        }

        var open = ConversationStatus.Open.ToString();
        var assigned = ConversationAssignmentStatus.Assigned.ToString();
        var ids = agentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return counts;
        }

        // Only the index is read: the counts are derived from the indexed columns, so no conversation document is
        // loaded to answer a question about how many there are.
        var rows = await Session.QueryIndex<MessagingConversationIndex>(
            index => index.AssignedAgentId.IsIn(ids) && index.Status == open && index.AssignmentStatus == assigned,
            collection: MessagingStorage.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var row in rows)
        {
            counts[row.AssignedAgentId] = counts.GetValueOrDefault(row.AssignedAgentId) + 1;
        }

        return counts;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MessagingConversation>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var queue = ConversationOwnerType.Queue.ToString();

        var conversations = await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => index.OwnerType == queue && index.OwnerId == queueId,
            collection: MessagingStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MessagingConversation>> GetFirstResponseOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var open = ConversationStatus.Open.ToString();

        // The deadline is indexed and cleared on reply, so the sweep seeks only the threads still owing one
        // rather than reading every open conversation and filtering them in memory.
        var conversations = await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => index.Status == open && index.FirstResponseDueUtc != null && index.FirstResponseDueUtc <= nowUtc,
            collection: MessagingStorage.CollectionName)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<DateTime?> GetNextFirstResponseDueUtcAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var open = ConversationStatus.Open.ToString();

        var conversation = await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => index.Status == open && index.FirstResponseDueUtc != null && index.FirstResponseDueUtc > nowUtc,
            collection: MessagingStorage.CollectionName)
            .OrderBy(index => index.FirstResponseDueUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return conversation?.FirstResponseDueUtc;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MessagingConversation>> GetRoutedAwaitingPickupAsync(CancellationToken cancellationToken = default)
    {
        var queue = ConversationOwnerType.Queue.ToString();
        var assigned = ConversationAssignmentStatus.Assigned.ToString();

        // AssignedUtc is indexed and cleared on pickup, so the sweep seeks only the threads still awaiting one
        // rather than reading every assigned department thread and filtering them in memory.
        var conversations = await Session.Query<MessagingConversation, MessagingConversationIndex>(
            index => index.OwnerType == queue && index.AssignmentStatus == assigned && index.AssignedUtc != null,
            collection: MessagingStorage.CollectionName)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MessagingConversation>> QueryAsync(MessagingInboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQuery<MessagingConversation> results = Build(query).OrderByDescending(index => index.LastMessageUtc);

        if (query.Skip > 0)
        {
            results = results.Skip(query.Skip);
        }

        if (query.Take > 0)
        {
            results = results.Take(query.Take);
        }

        var conversations = await results.ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(MessagingInboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Build(query).CountAsync(cancellationToken);
    }

    // The visibility rule and the tab are expressed as one predicate so the engine answers both from the index
    // rather than handing every thread back for the caller to filter.
    private IQuery<MessagingConversation, MessagingConversationIndex> Build(MessagingInboxQuery query)
    {
        var personal = ConversationOwnerType.Personal.ToString();
        var queueOwner = ConversationOwnerType.Queue.ToString();
        var assigned = ConversationAssignmentStatus.Assigned.ToString();
        var agentId = query.AgentId;
        var queueIds = (query.QueueIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        var results = Session.Query<MessagingConversation, MessagingConversationIndex>(collection: MessagingStorage.CollectionName);

        if (!query.IncludeAll)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                // No agent identity means nothing is owned or served, so nothing is visible.
                return results.Where(index => index.ItemId == null);
            }

            results = queueIds.Length == 0
                ? results.Where(index =>
                    index.AssignedAgentId == agentId ||
                    (index.OwnerType == personal && index.OwnerId == agentId))
                : results.Where(index =>
                    index.AssignedAgentId == agentId ||
                    (index.OwnerType == personal && index.OwnerId == agentId) ||
                    (index.OwnerType == queueOwner &&
                        index.OwnerId.IsIn(queueIds) &&
                        (index.AssignmentStatus != assigned || index.AssignedAgentId == null)));
        }

        if (!string.IsNullOrEmpty(query.Channel))
        {
            var channel = query.Channel;

            results = results.Where(index => index.Channel == channel);
        }

        return query.Filter switch
        {
            MessagingInboxFilter.Mine => results.Where(index => index.AssignmentStatus == assigned && index.AssignedAgentId == agentId),
            MessagingInboxFilter.Unassigned => results.Where(index => index.AssignmentStatus != assigned),
            _ => results,
        };
    }
}
