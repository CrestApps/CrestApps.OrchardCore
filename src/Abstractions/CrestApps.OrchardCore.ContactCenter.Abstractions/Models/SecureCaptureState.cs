namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the lifecycle state of an agent-assisted secure capture session.
/// </summary>
public enum SecureCaptureState
{
    /// <summary>
    /// The capture has been started by the agent and is waiting for the customer to open the secure page and
    /// submit their data.
    /// </summary>
    Collecting,

    /// <summary>
    /// The customer submitted the data, it was tokenized, and only the masked representation and the token
    /// reference were retained.
    /// </summary>
    Completed,

    /// <summary>
    /// The agent cancelled the capture before the customer completed it.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The capture window elapsed before the customer completed it, so the platform expired it.
    /// </summary>
    Expired,
}
