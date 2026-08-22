using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterEntryPointManager"/>.
/// </summary>
public sealed class ContactCenterEntryPointManager : CatalogManager<ContactCenterEntryPoint>, IContactCenterEntryPointManager
{
    private readonly IContactCenterEntryPointStore _store;
    private readonly IContactCenterConfigurationCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointManager"/> class.
    /// </summary>
    /// <param name="store">The underlying entry point store.</param>
    /// <param name="handlers">The catalog entry handlers for entry points.</param>
    /// <param name="cache">The routing configuration cache used to serve enabled entry points without re-querying the store.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterEntryPointManager(
        IContactCenterEntryPointStore store,
        IEnumerable<ICatalogEntryHandler<ContactCenterEntryPoint>> handlers,
        IContactCenterConfigurationCache cache,
        ILogger<CatalogManager<ContactCenterEntryPoint>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entryPoint = await _store.FindByNameAsync(name, cancellationToken);

        if (entryPoint is not null)
        {
            await LoadAsync(entryPoint, cancellationToken);
        }

        return entryPoint;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterEntryPoint>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetEnabledAsync(async token =>
        {
            var entryPoints = await _store.GetEnabledAsync(token);

            foreach (var entryPoint in entryPoints)
            {
                await LoadAsync(entryPoint, token);
            }

            return entryPoints;
        }, cancellationToken);
    }
}
