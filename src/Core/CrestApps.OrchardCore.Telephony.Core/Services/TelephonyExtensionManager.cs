using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ITelephonyExtensionManager"/>.
/// </summary>
public sealed class TelephonyExtensionManager : CatalogManager<TelephonyExtension>, ITelephonyExtensionManager
{
    private readonly ITelephonyExtensionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying extension store.</param>
    /// <param name="handlers">The catalog entry handlers for extensions.</param>
    /// <param name="logger">The logger instance.</param>
    public TelephonyExtensionManager(
        ITelephonyExtensionStore store,
        IEnumerable<ICatalogEntryHandler<TelephonyExtension>> handlers,
        ILogger<CatalogManager<TelephonyExtension>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<TelephonyExtension> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        var extension = await _store.FindByNumberAsync(number, cancellationToken);

        if (extension is not null)
        {
            await LoadAsync(extension, cancellationToken);
        }

        return extension;
    }

    /// <inheritdoc/>
    public async Task<TelephonyExtension> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var extension = await _store.FindByUserIdAsync(userId, cancellationToken);

        if (extension is not null)
        {
            await LoadAsync(extension, cancellationToken);
        }

        return extension;
    }
}
