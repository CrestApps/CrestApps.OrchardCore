using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterWorkStateManager"/>.
/// </summary>
public sealed class ContactCenterWorkStateManager : CatalogManager<ContactCenterWorkState>, IContactCenterWorkStateManager
{
    private readonly IContactCenterWorkStateStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateManager"/> class.
    /// </summary>
    /// <param name="store">The underlying work state store.</param>
    /// <param name="handlers">The catalog entry handlers for work state.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterWorkStateManager(
        IContactCenterWorkStateStore store,
        IEnumerable<ICatalogEntryHandler<ContactCenterWorkState>> handlers,
        ILogger<CatalogManager<ContactCenterWorkState>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterWorkState> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        var workState = await _store.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (workState is not null)
        {
            await LoadAsync(workState, cancellationToken);
        }

        return workState;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterWorkState>> GetByActivityIdsAsync(
        IEnumerable<string> activityItemIds,
        CancellationToken cancellationToken = default)
    {
        var states = await _store.GetByActivityIdsAsync(activityItemIds, cancellationToken);

        foreach (var state in states)
        {
            await LoadAsync(state, cancellationToken);
        }

        return states;
    }
}
