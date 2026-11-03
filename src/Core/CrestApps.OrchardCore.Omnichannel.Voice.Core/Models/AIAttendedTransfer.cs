namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A warm or screened transfer under way on an AI call: the side call ringing the destination while the caller
/// holds. Removed when the two are connected or the caller goes back to the AI.
/// </summary>
public sealed class AIAttendedTransfer
{
    /// <summary>
    /// Gets or sets the provider's identifier for the side call.
    /// </summary>
    public string SideCallId { get; set; }

    /// <summary>
    /// Gets or sets the number being rung.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets how the transfer is made.
    /// </summary>
    public AICallTransferMode Mode { get; set; }

    /// <summary>
    /// Gets or sets what whoever answers is told about the caller before they are connected.
    /// </summary>
    public string Briefing { get; set; }

    /// <summary>
    /// Gets or sets the line the caller hears once they are connected, or <see langword="null"/> for none.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hold audio is playing to the caller.
    /// </summary>
    public bool HoldAudioPlaying { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a person answered the side call.
    /// </summary>
    public bool Answered { get; set; }

    /// <summary>
    /// Gets or sets when the side call was placed.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
