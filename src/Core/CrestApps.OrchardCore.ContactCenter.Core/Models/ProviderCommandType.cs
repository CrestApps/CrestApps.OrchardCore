namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the kind of provider operation a <see cref="ProviderCommand"/> represents.
/// </summary>
public enum ProviderCommandType
{
    /// <summary>
    /// An outbound dial request. This is the customer-action-risk command the state machine protects first.
    /// </summary>
    Dial,

    /// <summary>
    /// A request to answer or connect an existing inbound call.
    /// </summary>
    Answer,

    /// <summary>
    /// A request to end an active call.
    /// </summary>
    Hangup,

    /// <summary>
    /// A request to transfer an active call.
    /// </summary>
    Transfer,

    /// <summary>
    /// A request to place an active call on hold.
    /// </summary>
    Hold,

    /// <summary>
    /// A request to resume a held call.
    /// </summary>
    Resume,
}
