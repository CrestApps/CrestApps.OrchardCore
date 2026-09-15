namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the provider's knowledge of a previously sent voice command.
/// </summary>
public enum ContactCenterVoiceCommandReconciliationOutcome
{
    /// <summary>
    /// The provider confirmed that the command executed.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The provider authoritatively confirmed that the command did not execute.
    /// </summary>
    NotExecuted,

    /// <summary>
    /// The provider cannot prove whether the command executed.
    /// </summary>
    Unknown,
}
