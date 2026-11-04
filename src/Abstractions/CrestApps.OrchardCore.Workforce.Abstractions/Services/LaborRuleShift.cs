namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A shift as the labor rules see it.
/// </summary>
public sealed class LaborRuleShift
{
    /// <summary>
    /// Gets or sets the shift's key.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the day it starts, in the team's time zone.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets when it starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets its paid minutes.
    /// </summary>
    public int PaidMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes of breaks and meals inside it.
    /// </summary>
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Gets or sets the skills it needs.
    /// </summary>
    public IReadOnlyCollection<string> RequiredSkillIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether it is one of the shifts being checked, rather than one already worked or planned.
    /// </summary>
    public bool IsCandidate { get; set; }
}
