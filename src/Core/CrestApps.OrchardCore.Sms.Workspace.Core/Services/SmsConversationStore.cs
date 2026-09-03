using CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

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
        CollectionName = SmsWorkspaceStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.ServiceAddress == serviceAddress && index.ContactAddress == contactAddress,
            collection: SmsWorkspaceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByContactAsync(string contactAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contactAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
                index => index.ContactAddress == contactAddress,
                collection: SmsWorkspaceStorage.CollectionName)
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
            collection: SmsWorkspaceStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var queue = SmsConversationOwnerType.Queue.ToString();

        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.OwnerType == queue && index.OwnerId == queueId,
            collection: SmsWorkspaceStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsConversation>> GetRoutedAwaitingPickupAsync(CancellationToken cancellationToken = default)
    {
        var queue = SmsConversationOwnerType.Queue.ToString();
        var assigned = SmsConversationAssignmentStatus.Assigned.ToString();

        // AssignedUtc is not indexed (it is cleared on pickup), so query the indexed queue-assigned set and let
        // the caller filter by age. The set is small in practice — only routed conversations awaiting pickup.
        var conversations = await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.OwnerType == queue && index.AssignmentStatus == assigned,
            collection: SmsWorkspaceStorage.CollectionName)
            .ListAsync(cancellationToken);

        return conversations
            .Where(conversation => conversation.AssignedUtc is not null)
            .ToArray();
    }
}
