using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderCommandManager"/>.
/// </summary>
public sealed class ProviderCommandManager : CatalogManager<ProviderCommand>, IProviderCommandManager
{
    private readonly IProviderCommandStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandManager"/> class.
    /// </summary>
    /// <param name="store">The underlying provider command store.</param>
    /// <param name="handlers">The catalog entry handlers for provider commands.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderCommandManager(
        IProviderCommandStore store,
        IEnumerable<ICatalogEntryHandler<ProviderCommand>> handlers,
        ILogger<CatalogManager<ProviderCommand>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ProviderCommand> FindByCommandIdAsync(string commandId, CancellationToken cancellationToken = default)
    {
        var command = await _store.FindByCommandIdAsync(commandId, cancellationToken);

        if (command is not null)
        {
            await LoadAsync(command, cancellationToken);
        }

        return command;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderCommand>> ListDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var commands = await _store.ListDueAsync(nowUtc, maxCount, cancellationToken);

        foreach (var command in commands)
        {
            await LoadAsync(command, cancellationToken);
        }

        return commands;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ProviderCommand>> ListReclaimableAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var commands = await _store.ListReclaimableAsync(nowUtc, maxCount, cancellationToken);

        foreach (var command in commands)
        {
            await LoadAsync(command, cancellationToken);
        }

        return commands;
    }
}
