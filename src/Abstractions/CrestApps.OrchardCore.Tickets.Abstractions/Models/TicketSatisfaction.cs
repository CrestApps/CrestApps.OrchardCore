namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The requester's rating of how their request was handled: one per ticket, given once it is solved.
/// </summary>
public sealed class TicketSatisfaction
{
    /// <summary>
    /// The lowest score.
    /// </summary>
    public const int MinScore = 1;

    /// <summary>
    /// The highest score.
    /// </summary>
    public const int MaxScore = 5;

    /// <summary>
    /// The longest comment kept.
    /// </summary>
    public const int MaxCommentLength = 2000;

    /// <summary>
    /// Gets or sets the score, from <see cref="MinScore"/> to <see cref="MaxScore"/>.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets what the requester added, if anything.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets when they rated it.
    /// </summary>
    public DateTime RatedUtc { get; set; }
}
