namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A page of messages read from the queue shared voicemail boxes, newest first.
/// </summary>
public sealed class SharedVoicemailPage
{
    /// <summary>
    /// Gets an empty page.
    /// </summary>
    public static SharedVoicemailPage Empty { get; } = new();

    /// <summary>
    /// Gets or sets the number of messages that match the query across every page.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the messages on this page.
    /// </summary>
    public IReadOnlyList<SharedVoicemail> Entries { get; set; } = [];
}
