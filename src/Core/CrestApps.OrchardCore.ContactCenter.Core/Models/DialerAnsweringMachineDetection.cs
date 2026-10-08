namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Whether an automated dialer asks the provider to tell a person from an answering machine before connecting an agent.
/// </summary>
public enum DialerAnsweringMachineDetection
{
    /// <summary>
    /// No detection: the agent is connected the moment the call is answered, whoever answered it.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The provider's standard detection.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// The provider's premium detection, which is more accurate and costs more per call where the provider charges for it.
    /// </summary>
    Premium = 2,
}
