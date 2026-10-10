namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// What one turn of an automated conversation ended with.
/// </summary>
internal sealed class AutomatedTurnOutcome
{
    /// <summary>
    /// Gets or sets a value indicating whether the turn was handled: a reply was sent, or the agent chose to send none.
    /// Either way the activity advances and the conclusion check runs.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model ended the conversation with the hang-up marker.
    /// </summary>
    public bool HangupRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model asked to hand the customer to a person.
    /// </summary>
    public bool HandoffRequested { get; set; }

    /// <summary>
    /// Gets or sets the reason the model gave for the handoff.
    /// </summary>
    public string HandoffReason { get; set; }
}
