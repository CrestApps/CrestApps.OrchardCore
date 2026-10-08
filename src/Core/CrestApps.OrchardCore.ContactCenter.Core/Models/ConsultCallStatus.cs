namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the lifecycle state of a consult call placed before a warm transfer.
/// </summary>
public enum ConsultCallStatus
{
    /// <summary>
    /// The consult has been requested but the destination has not been reached.
    /// </summary>
    Initiated,

    /// <summary>
    /// The consult destination is alerting.
    /// </summary>
    Ringing,

    /// <summary>
    /// The consulting agent and the destination are talking privately.
    /// </summary>
    Connected,

    /// <summary>
    /// The consult ended by completing the transfer to the consulted destination.
    /// </summary>
    Completed,

    /// <summary>
    /// The consulting agent abandoned the consult and returned to the customer.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The consult could not be established.
    /// </summary>
    Failed,
}
