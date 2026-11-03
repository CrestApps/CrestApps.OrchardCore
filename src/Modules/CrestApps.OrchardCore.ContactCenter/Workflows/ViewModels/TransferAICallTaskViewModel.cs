using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Edits the Transfer AI Call workflow task.
/// </summary>
public class TransferAICallTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression of the call's activity.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression of the number to ring.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets how the call is transferred.
    /// </summary>
    public AICallTransferMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template of the briefing.
    /// </summary>
    public string Briefing { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template of the caller's introduction.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets how long the number rings.
    /// </summary>
    public int RingTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the hold audio.
    /// </summary>
    public string HoldAudio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person rung sees the caller's number.
    /// </summary>
    public bool ShowCallerNumber { get; set; }
}
