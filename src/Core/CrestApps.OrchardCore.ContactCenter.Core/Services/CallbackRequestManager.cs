using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ICallbackRequestManager"/>.
/// </summary>
public sealed class CallbackRequestManager : CatalogManager<CallbackRequest>, ICallbackRequestManager
{
    private readonly ICallbackRequestStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestManager"/> class.
    /// </summary>
    /// <param name="store">The underlying callback store.</param>
    /// <param name="handlers">The catalog entry handlers for callbacks.</param>
    /// <param name="logger">The logger instance.</param>
    public CallbackRequestManager(
        ICallbackRequestStore store,
        IEnumerable<ICatalogEntryHandler<CallbackRequest>> handlers,
        ILogger<CatalogManager<CallbackRequest>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<CallbackRequest>> ListDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default)
    {
        var callbacks = await _store.ListDueAsync(utcNow, maxCount, cancellationToken);

        foreach (var callback in callbacks)
        {
            await LoadAsync(callback, cancellationToken);
        }

        return callbacks;
    }
}
