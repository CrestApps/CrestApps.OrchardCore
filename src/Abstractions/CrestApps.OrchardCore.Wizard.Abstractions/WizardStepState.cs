namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The lifecycle state of a single <see cref="WizardStep"/> within a wizard session. Completion is tracked
/// explicitly rather than being inferred from the presence of saved data so that long-running and
/// externally-completed steps (for example a redirect to a third-party service) can be modeled safely.
/// </summary>
public enum WizardStepState
{
    /// <summary>
    /// The step has not been started yet.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// The step is the current step and is being worked on.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// The step has been completed and, when applicable, its data has been saved.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The step failed and must be revisited before the wizard can complete.
    /// </summary>
    Failed = 3,
}
