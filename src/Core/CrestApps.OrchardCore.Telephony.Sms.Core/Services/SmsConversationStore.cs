using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

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
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByAddressesAsync(string serviceAddress, string customerAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceAddress);
        ArgumentException.ThrowIfNullOrEmpty(customerAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
            index => index.ServiceAddress == serviceAddress && index.CustomerAddress == customerAddress,
            collection: TelephonySmsStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> FindByCustomerAsync(string customerAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerAddress);

        return await Session.Query<SmsConversation, SmsConversationIndex>(
                index => index.CustomerAddress == customerAddress,
                collection: TelephonySmsStorage.CollectionName)
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
            collection: TelephonySmsStorage.CollectionName)
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
            collection: TelephonySmsStorage.CollectionName)
            .OrderByDescending(index => index.LastMessageUtc)
            .ListAsync(cancellationToken);

        return conversations.ToArray();
    }
}
