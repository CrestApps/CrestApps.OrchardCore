namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The result of a wizard completion attempt performed by <see cref="IWizardEngine"/>.
/// </summary>
public sealed class WizardCompletionResult
{
    /// <summary>
    /// Gets the outcome of the completion attempt.
    /// </summary>
    public WizardCompletionStatus Status { get; init; }

    /// <summary>
    /// Gets the key of the data-collecting step that blocked completion, when
    /// <see cref="Status"/> is <see cref="WizardCompletionStatus.Blocked"/>.
    /// </summary>
    public string BlockingStepKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the wizard is considered complete after the attempt, whether it was
    /// completed by this attempt or an earlier one.
    /// </summary>
    public bool IsCompleted
        => Status is WizardCompletionStatus.Completed or WizardCompletionStatus.AlreadyCompleted;
}
