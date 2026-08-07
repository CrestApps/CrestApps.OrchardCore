using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for provider commands.
/// </summary>
public interface IProviderCommandManager : ICatalogManager<ProviderCommand>
{
    /// <summary>
    /// Finds a command by its stable idempotency key.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command, or <see langword="null"/> when none exists.</returns>
    Task<ProviderCommand> FindByCommandIdAsync(string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the commands that are due for dispatch, reconciliation, or compensation recovery, ordered by their due time.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="maxCount">The maximum number of commands to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The commands whose next attempt time has elapsed.</returns>
    Task<IReadOnlyCollection<ProviderCommand>> ListDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the claimed or sent commands whose lease has expired and can be reclaimed.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="maxCount">The maximum number of commands to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The commands whose lease has expired.</returns>
    Task<IReadOnlyCollection<ProviderCommand>> ListReclaimableAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default);
}
