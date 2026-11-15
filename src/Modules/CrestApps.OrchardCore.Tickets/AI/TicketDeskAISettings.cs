namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// A desk's AI assistant, kept in the desk's properties when AI is on.
/// </summary>
public sealed class TicketDeskAISettings
{
    /// <summary>
    /// Gets or sets the chat AI profile that triages the desk's tickets and suggests replies, with its instructions,
    /// knowledge and model; <see langword="null"/> turns the assistant off for the desk.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets whether a triage sets the priority it suggests, rather than only noting it.
    /// </summary>
    public bool ApplyPriority { get; set; }
}
