namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// A desk's AI assistant, kept in the desk's properties when AI is on.
/// </summary>
public sealed class TicketDeskAISettings
{
    /// <summary>
    /// Gets or sets the chat AI profile that triages the desk's tickets and suggests replies, with its instructions,
    /// knowledge and model; <see langword="null"/> uses the site's (<see cref="TicketAISettings.ReplyProfileId"/>).
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets whether the desk has no assistant, even when the site names a profile.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets whether a triage sets the priority it suggests, rather than only noting it.
    /// </summary>
    public bool ApplyPriority { get; set; }
}
