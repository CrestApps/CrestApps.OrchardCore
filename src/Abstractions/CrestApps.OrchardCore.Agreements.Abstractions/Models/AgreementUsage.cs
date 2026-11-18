namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// One entry of time spent under an agreement, as a usage provider reports it.
/// </summary>
public sealed class AgreementUsage
{
    /// <summary>
    /// Gets or sets the usage provider that reported it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets what the time was spent on, such as a ticket's record (<c>Ticket:{id}</c>).
    /// </summary>
    public string SourceRef { get; set; }

    /// <summary>
    /// Gets or sets what the time was spent on, as people read it, such as "T-42 VPN drops".
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets who spent it.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets their name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets when the time was spent, in UTC; it counts in the period that holds this moment.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets how long, in minutes, before the agreement's rounding.
    /// </summary>
    public double Minutes { get; set; }
}
