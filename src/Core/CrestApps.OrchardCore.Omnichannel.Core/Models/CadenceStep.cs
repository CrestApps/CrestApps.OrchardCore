namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents a single step in a <see cref="Cadence"/>: one follow-up message sent to a contact who has gone
/// quiet, after a configured amount of silence.
/// </summary>
public sealed class CadenceStep
{
    /// <summary>
    /// Gets or sets how long, in minutes, the contact must be silent since the automation's previous message before
    /// this step's nudge is sent. The clock restarts after each nudge, so successive steps space nudges further out.
    /// </summary>
    public int DelayMinutes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI composes this nudge. When <see langword="true"/>, the AI writes
    /// the message (using <see cref="Message"/> as optional guidance when provided); when <see langword="false"/>,
    /// <see cref="Message"/> is sent to the contact verbatim.
    /// </summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>
    /// Gets or sets the message. When <see cref="IsAiGenerated"/> is <see langword="false"/> this is the exact text
    /// sent to the contact; when <see langword="true"/> it is optional guidance the AI composes the nudge from.
    /// </summary>
    public string Message { get; set; }
}
