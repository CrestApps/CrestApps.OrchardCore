using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="ISmsConversationStore"/>.
/// </summary>
public sealed class SmsConversationStore : DocumentCatalog<SmsConversation, SmsConversationIndex>, ISmsConversationStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SmsConversationStore(ISession session)
        : base(session)
    {
        CollectionName = SmsPortalStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.ServiceAddress == serviceAddress && index.ContactAddress == contactAddress,
            collection: SmsPortalStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByContactAsync(string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
                index => index.ContactAddress == contactAddress,
                collection: SmsPortalStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var personal = SmsConversationOwnerType.Personal.ToString();

        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => (index.AssignedAgentId == agentId || (index.OwnerType == personal && index.OwnerId == agentId)),
            collection: SmsPortalStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public Task<int> CountOpenAssignedAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var open = SmsConversationStatus.Open.ToString();
        var assigned = SmsConversationAssignmentStatus.Assigned.ToString();

        return Session.QueryIndex<SmsConversationIndex>(
            index => index.AssignedAgentId == agentId && index.Status == open && index.AssignmentStatus == assigned,
            collection: SmsPortalStorage.CollectionName)
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

        var open = SmsConversationStatus.Open.ToString();
        var assigned = SmsConversationAssignmentStatus.Assigned.ToString();
        var ids = agentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return counts;
        }

        // Only the index is read: the counts are derived from the indexed columns, so no conversation document is
        // loaded to answer a question about how many there are.
        var rows = await Session.QueryIndex<SmsConversationIndex>(
            index => index.AssignedAgentId.IsIn(ids) && index.Status == open && index.AssignmentStatus == assigned,
            collection: SmsPortalStorage.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var row in rows)
        {
            counts[row.AssignedAgentId] = counts.GetValueOrDefault(row.AssignedAgentId) + 1;
        }

        return counts;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var queue = SmsConversationOwnerType.Queue.ToString();

        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.OwnerType == queue && index.OwnerId == queueId,
            collection: SmsPortalStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetFirstResponseOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var open = SmsConversationStatus.Open.ToString();

        // The deadline is indexed and cleared on reply, so the sweep seeks only the threads still owing one
        // rather than reading every open conversation and filtering them in memory.
        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.Status == open && index.FirstResponseDueUtc != null && index.FirstResponseDueUtc <= nowUtc,
            collection: SmsPortalStorage.CollectionName)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetRoutedAwaitingPickupAsync(CancellationToken cancellationToken = default)
    {
        var queue = SmsConversationOwnerType.Queue.ToString();
        var assigned = SmsConversationAssignmentStatus.Assigned.ToString();

        // AssignedUtc is indexed and cleared on pickup, so the sweep seeks only the threads still awaiting one
        // rather than reading every assigned department thread and filtering them in memory.
        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.OwnerType == queue && index.AssignmentStatus == assigned && index.AssignedUtc != null,
            collection: SmsPortalStorage.CollectionName)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SmsConversation>> QueryAsync(SmsInboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQuery<SmsConversation> results = Build(query).OrderByDescending(index => index.LastMessageUtc);

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
    public Task<int> CountAsync(SmsInboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Build(query).CountAsync(cancellationToken);
    }

    // The visibility rule and the tab are expressed as one predicate so the engine answers both from the index
    // rather than handing every thread back for the caller to filter.
    private IQuery<SmsConversation, SmsConversationIndex> Build(SmsInboxQuery query)
    {
        var personal = SmsConversationOwnerType.Personal.ToString();
        var queueOwner = SmsConversationOwnerType.Queue.ToString();
        var assigned = SmsConversationAssignmentStatus.Assigned.ToString();
        var agentId = query.AgentId;
        var queueIds = (query.QueueIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        var results = Session.Query<SmsConversation, SmsConversationIndex>(collection: SmsPortalStorage.CollectionName);

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

        return query.Filter switch
        {
            SmsInboxFilter.Mine => results.Where(index => index.AssignmentStatus == assigned && index.AssignedAgentId == agentId),
            SmsInboxFilter.Unassigned => results.Where(index => index.AssignmentStatus != assigned),
            _ => results,
        };
    }
}
