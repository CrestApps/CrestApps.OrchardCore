namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Orchestrates wizard completion. It serializes finalization for a session under a distributed lock, is
/// idempotent for concurrent or repeated attempts, and supports a prepare-under-lock callback so a consumer
/// (for example a payment redirect return) can validate external evidence and record it inside the same
/// lock before the wizard is finalized.
/// </summary>
public interface IWizardEngine
{
    /// <summary>
    /// Attempts to complete the wizard. The engine acquires a distributed lock for the session, reloads the
    /// authoritative session, verifies every data-collecting step has data, optionally runs
    /// <paramref name="prepareUnderLock"/>, then runs the completing and completed handlers exactly once.
    /// </summary>
    /// <param name="flow">The wizard flow to complete.</param>
    /// <param name="prepareUnderLock">
    /// An optional callback invoked inside the lock after step validation and before finalization. It
    /// receives the authoritative flow and returns <see langword="true"/> to proceed or <see langword="false"/>
    /// to abort the completion. Use it to validate and record external step evidence.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The completion result.</returns>
    Task<WizardCompletionResult> CompleteAsync(
        WizardFlow flow,
        Func<WizardFlow, Task<bool>> prepareUnderLock = null,
        CancellationToken cancellationToken = default);
}
