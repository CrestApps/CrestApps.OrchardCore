using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Dispatches durable provider commands and safely recovers commands that require dispatch, reconciliation,
/// or compensation.
/// </summary>
public interface IProviderCommandProcessor
{
    /// <summary>
    /// Processes the command identified by its stable idempotency key.
    /// </summary>
    /// <param name="commandId">The stable idempotency key of the command to process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command after its attempted processing.</returns>
    Task<ProviderCommand> DispatchAsync(string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escalates expired leases and processes commands due for dispatch, reconciliation, or compensation.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of due commands whose processing was attempted.</returns>
    Task<int> RecoverDueAsync(CancellationToken cancellationToken = default);
}
