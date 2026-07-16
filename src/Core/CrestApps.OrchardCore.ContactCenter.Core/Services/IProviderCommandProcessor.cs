using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

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
    /// Settles a provider dispatch response in the current fresh tenant scope after validating the claim fence.
    /// </summary>
    /// <param name="commandId">The stable idempotency key of the command.</param>
    /// <param name="claim">The ownership claim that sent the provider request.</param>
    /// <param name="result">The provider response, or <see langword="null"/> when no reliable response was returned.</param>
    /// <param name="outcomeUnknownReason">The durable reason used when the result cannot prove a terminal outcome.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command after fenced settlement.</returns>
    Task<ProviderCommand> SettleDispatchAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceProviderResult result,
        string outcomeUnknownReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a provider reconciliation response in the current fresh tenant scope after validating the claim fence.
    /// </summary>
    /// <param name="commandId">The stable idempotency key of the command.</param>
    /// <param name="claim">The ownership claim that performed reconciliation.</param>
    /// <param name="result">The provider reconciliation response, or <see langword="null"/> when no reliable response was returned.</param>
    /// <param name="inconclusiveReason">The durable reason used when reconciliation cannot prove a terminal outcome.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The command after fenced settlement.</returns>
    Task<ProviderCommand> SettleReconciliationAsync(
        string commandId,
        ProviderCommandClaim claim,
        ContactCenterVoiceCommandReconciliationResult result,
        string inconclusiveReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escalates expired leases and processes commands due for dispatch, reconciliation, or compensation.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of due commands whose processing was attempted.</returns>
    Task<int> RecoverDueAsync(CancellationToken cancellationToken = default);
}
