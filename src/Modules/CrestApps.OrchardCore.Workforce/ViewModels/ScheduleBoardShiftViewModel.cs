namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One shift on the planner board.
/// </summary>
public class ScheduleBoardShiftViewModel
{
    /// <summary>
    /// Gets or sets the draft shift id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets when it starts, on the wall clock of the team.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets the shift template it came from.
    /// </summary>
    public string TemplateName { get; set; }

    /// <summary>
    /// Gets or sets its color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets its breaks and activities, as text.
    /// </summary>
    public string Segments { get; set; }
}
