using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Coordinates the durable provider-command state machine. Every transition is validated against the legal
/// transition graph and, where required, against the command's fence token and owner token so a superseded
/// worker, a lost provider response, or a retry can never cause a duplicate customer action. A command whose
/// outcome cannot be proven is paused rather than re-sent.
/// </summary>
public interface IProviderCommandStateService
{
    /// <summary>
    /// Registers a new command in the <see cref="ProviderCommandStatus.Pending"/> state, or returns the
    /// existing command when one already exists for the same idempotency key.
    /// </summary>
    /// <param name="registration">The command registration intent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The registered or pre-existing command.</returns>
    Task<ProviderCommand> RegisterAsync(ProviderCommandRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a fenced, exclusive lease over the command so it can be sent to the provider.
    /// A pending command, or a claimed command whose lease has expired, can be claimed.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="leaseDuration">The duration the acquired lease remains valid.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The acquired claim, or <see langword="null"/> when the command cannot be claimed.</returns>
    Task<ProviderCommandClaim> TryClaimAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a claimed command as sent to the provider. The presented claim must match the command's current
    /// fence and owner tokens.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="providerReference">The provider-assigned reference, when already known.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated command.</returns>
    Task<ProviderCommand> MarkSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms that a sent command executed. The presented claim must match the command's current fence and
    /// owner tokens.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="providerReference">The provider-assigned reference captured on confirmation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The confirmed command.</returns>
    Task<ProviderCommand> ConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages confirmation of a sent command without committing the tenant session so outcome projections can
    /// be persisted in the same database transaction.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="providerReference">The provider-assigned reference captured on confirmation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The staged confirmed command.</returns>
    Task<ProviderCommand> StageConfirmSentAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a sent command as having an unknown outcome because the provider response was lost or ambiguous.
    /// The command must be reconciled before any retry and is never re-sent directly from this state.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="reason">The reason the outcome is unknown.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated command.</returns>
    Task<ProviderCommand> MarkOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an unknown provider outcome without committing the tenant session so related projections can be
    /// persisted in the same database transaction.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="reason">The reason the outcome is unknown.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The staged command.</returns>
    Task<ProviderCommand> StageOutcomeUnknownAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escalates a command whose lease has expired. A sent command becomes
    /// <see cref="ProviderCommandStatus.OutcomeUnknown"/> so it is reconciled before retry, while a claimed
    /// command that was never sent returns to <see cref="ProviderCommandStatus.Pending"/>.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated command.</returns>
    Task<ProviderCommand> EscalateExpiredLeaseAsync(string commandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire the exclusive lease used for provider reconciliation.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="leaseDuration">The duration of the reconciliation lease.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The acquired claim, or <see langword="null"/> when another worker owns reconciliation.</returns>
    Task<ProviderCommandClaim> TryClaimReconciliationAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a command from reconciliation while the caller owns the reconciliation fence.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The reconciliation claim held by the caller.</param>
    /// <param name="providerReference">The provider-assigned reference captured during reconciliation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The confirmed command.</returns>
    Task<ProviderCommand> ConfirmFromReconciliationAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages reconciliation confirmation without committing the tenant session so outcome projections can be
    /// persisted in the same database transaction.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The reconciliation claim held by the caller.</param>
    /// <param name="providerReference">The provider-assigned reference captured during reconciliation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The staged confirmed command.</returns>
    Task<ProviderCommand> StageConfirmFromReconciliationAsync(string commandId, ProviderCommandClaim claim, string providerReference = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins compensating a pending command whose request cannot be dispatched.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="reason">The reason compensation is required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated command.</returns>
    Task<ProviderCommand> BeginPendingCompensationAsync(string commandId, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins compensation while the caller still owns the dispatch or reconciliation fence.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The claim held by the caller.</param>
    /// <param name="reason">The reason compensation is required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The compensating command.</returns>
    Task<ProviderCommand> BeginCompensationAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to acquire a fenced, exclusive lease for a command that is awaiting compensation.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="leaseDuration">The duration the acquired compensation lease remains valid.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The acquired claim, or <see langword="null"/> when compensation is already owned.</returns>
    Task<ProviderCommandClaim> TryClaimCompensationAsync(string commandId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes compensation, moving the command to the terminal
    /// <see cref="ProviderCommandStatus.Compensated"/> state.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The compensation claim held by the caller.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The compensated command.</returns>
    Task<ProviderCommand> CompleteCompensationAsync(string commandId, ProviderCommandClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses reconciliation while the caller owns the reconciliation fence.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="claim">The reconciliation claim held by the caller.</param>
    /// <param name="reason">The reason automated processing cannot continue safely.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The paused command.</returns>
    Task<ProviderCommand> PauseAsync(string commandId, ProviderCommandClaim claim, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails a command definitively, moving it to the terminal <see cref="ProviderCommandStatus.Failed"/>
    /// state.
    /// </summary>
    /// <param name="commandId">The stable idempotency key.</param>
    /// <param name="reason">The reason the command failed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The failed command.</returns>
    Task<ProviderCommand> FailAsync(string commandId, string reason = null, CancellationToken cancellationToken = default);
}
