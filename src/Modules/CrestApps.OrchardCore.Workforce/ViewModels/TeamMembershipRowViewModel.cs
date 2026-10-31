namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One team membership row in the worker editor.
/// </summary>
public class TeamMembershipRowViewModel
{
    /// <summary>
    /// Gets or sets the team; empty to leave the row out.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the role in the team.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }
}
