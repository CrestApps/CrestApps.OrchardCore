using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IInteractionStore"/>.
/// </summary>
public sealed class InteractionStore : IInteractionStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public InteractionStore(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await _session.SaveAsync(interaction, ContactCenterConstants.CollectionName);
    }

    /// <inheritdoc/>
    public async ValueTask UpdateAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await _session.SaveAsync(interaction, ContactCenterConstants.CollectionName);
    }

    /// <inheritdoc/>
    public async ValueTask<Interaction> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session.Query<Interaction, InteractionIndex>(
            index => index.ItemId == id,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        return await _session.Query<Interaction, InteractionIndex>(
            index => index.ActivityItemId == activityItemId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

        return await _session.Query<Interaction, InteractionIndex>(
            index => index.CorrelationId == correlationId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<Interaction, InteractionIndex>(
            index => index.Status == status,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<Interaction>
        {
            Count = await query.CountAsync(cancellationToken),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }
}
