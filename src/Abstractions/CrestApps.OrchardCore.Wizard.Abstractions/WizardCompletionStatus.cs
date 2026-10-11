namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The outcome of a wizard completion attempt.
/// </summary>
public enum WizardCompletionStatus
{
    /// <summary>
    /// The wizard was completed by this attempt.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// The wizard was already completed by an earlier attempt, so this attempt was a safe no-op.
    /// </summary>
    AlreadyCompleted = 1,

    /// <summary>
    /// The wizard could not complete because a data-collecting step is still incomplete. The blocking step
    /// is exposed on <see cref="WizardCompletionResult.BlockingStepKey"/>.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The completion attempt failed. A prepare-under-lock callback returned <see langword="false"/> or a
    /// handler reported failure.
    /// </summary>
    Failed = 3,
}
