namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The service desk's AI settings (Settings &gt; Service Desk, with AI on), kept in the site settings.
/// </summary>
public sealed class TicketAISettings
{
    /// <summary>
    /// Gets or sets the chat AI profile that suggests replies to agents and triages tickets on every desk that names no
    /// profile of its own (<see cref="TicketDeskAISettings"/>); <see langword="null"/> leaves those desks without an
    /// assistant.
    /// </summary>
    public string ReplyProfileId { get; set; }
}
