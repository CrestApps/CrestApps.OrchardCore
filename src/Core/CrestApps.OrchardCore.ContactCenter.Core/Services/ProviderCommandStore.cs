using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-backed provider command store. Updates use document-version optimistic concurrency so
/// two workers racing on the same command cannot both win a transition.
/// </summary>
public sealed class ProviderCommandStore : DocumentCatalog<ProviderCommand, ProviderCommandIndex>, IProviderCommandStore
{
    /// <summary>
    /// The default maximum number of commands returned by a batch query.
    /// </summary>
    public const int DefaultBatchSize = 100;

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    public ProviderCommandStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> FindByCommandIdAsync(string commandId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);

        return await Session.Query<ProviderCommand, ProviderCommandIndex>(
            index => index.CommandId == commandId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderCommand>> ListDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var commands = await Session.Query<ProviderCommand, ProviderCommandIndex>(
            index => (index.Status == ProviderCommandStatus.Pending ||
                index.Status == ProviderCommandStatus.OutcomeUnknown ||
                index.Status == ProviderCommandStatus.Compensating) &&
                index.NextAttemptUtc <= nowUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return commands.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderCommand>> ListReclaimableAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var commands = await Session.Query<ProviderCommand, ProviderCommandIndex>(
            index => (index.Status == ProviderCommandStatus.Claimed || index.Status == ProviderCommandStatus.Sent) &&
                index.LeaseExpiresUtc <= nowUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.LeaseExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return commands.ToArray();
    }
}
