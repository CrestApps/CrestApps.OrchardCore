namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// Notes a supervisor sent the AI on a turn-based call, kept on the activity until the AI's next turn reads them.
/// </summary>
public sealed class PendingSupervisorNotes
{
    /// <summary>
    /// Gets or sets the notes, oldest first.
    /// </summary>
    public List<string> Notes { get; set; } = [];
}
