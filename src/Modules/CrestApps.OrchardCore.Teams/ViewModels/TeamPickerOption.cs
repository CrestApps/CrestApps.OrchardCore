namespace CrestApps.OrchardCore.Teams.ViewModels;

/// <summary>
/// A team the team picker offers.
/// </summary>
public sealed class TeamPickerOption
{
    /// <summary>
    /// Gets or sets the team id.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the team name.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets how deep the team is in the tree; a top team is 0.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets whether the team is chosen.
    /// </summary>
    public bool Selected { get; set; }
}
