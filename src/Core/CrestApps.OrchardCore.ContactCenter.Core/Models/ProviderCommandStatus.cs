namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the durable lifecycle state of a provider command as it moves from creation through provider
/// execution, outcome confirmation, and terminal resolution.
/// </summary>
public enum ProviderCommandStatus
{
    /// <summary>
    /// The command intent is persisted but has not yet been claimed by a worker for dispatch.
    /// </summary>
    Pending,

    /// <summary>
    /// A worker holds an exclusive, fenced lease on the command and may send it to the provider.
    /// </summary>
    Claimed,

    /// <summary>
    /// The command was dispatched to the provider and the outcome has not yet been confirmed.
    /// </summary>
    Sent,

    /// <summary>
    /// The provider response was lost or ambiguous, so the outcome must be reconciled before any retry.
    /// The command is never re-sent directly from this state.
    /// </summary>
    OutcomeUnknown,

    /// <summary>
    /// The provider confirmed the command executed. This is a terminal success state.
    /// </summary>
    Confirmed,

    /// <summary>
    /// Reconciliation proved the command did not execute (or must be undone) and compensation is in progress.
    /// </summary>
    Compensating,

    /// <summary>
    /// Compensation completed. This is a terminal state.
    /// </summary>
    Compensated,

    /// <summary>
    /// The command failed definitively. This is a terminal state.
    /// </summary>
    Failed,

    /// <summary>
    /// The outcome could not be proven and automatic retry is suspended to avoid a duplicate customer action.
    /// The command waits for reconciliation or an operator decision.
    /// </summary>
    Paused,
}
