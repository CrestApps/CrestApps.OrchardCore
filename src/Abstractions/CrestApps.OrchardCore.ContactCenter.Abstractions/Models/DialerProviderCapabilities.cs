namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the calling capabilities exposed by a dialer provider.
/// </summary>
[Flags]
public enum DialerProviderCapabilities
{
    /// <summary>
    /// The provider exposes no calling capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider can place an outbound call for a reserved activity.
    /// </summary>
    Outbound = 1 << 0,

    /// <summary>
    /// The provider can present a caller identifier when placing a call.
    /// </summary>
    CallerId = 1 << 1,

    /// <summary>
    /// The provider can detect an answering machine before connecting an agent.
    /// </summary>
    AnsweringMachineDetection = 1 << 2,

    /// <summary>
    /// The provider can cancel an in-progress outbound attempt.
    /// </summary>
    Cancellation = 1 << 3,
}
