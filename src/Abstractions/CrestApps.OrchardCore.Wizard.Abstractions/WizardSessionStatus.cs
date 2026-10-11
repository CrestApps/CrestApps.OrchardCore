namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// The lifecycle state of a <see cref="WizardSession"/>.
/// </summary>
public enum WizardSessionStatus
{
    /// <summary>
    /// The wizard has been created and the user is still working through the flow steps.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The wizard was submitted and an external, asynchronous step (for example a redirect to a third-party
    /// service) has been engaged, but no confirmation has been received yet. The session must never finalize
    /// while in this state.
    /// </summary>
    AwaitingExternal = 1,

    /// <summary>
    /// Every step has been completed and the wizard has finalized.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The wizard failed (for example a finalization error or an external step that reported failure).
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The user or the system canceled the wizard before it completed.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// The wizard was abandoned and has passed its maximum lifetime.
    /// </summary>
    Expired = 5,
}
