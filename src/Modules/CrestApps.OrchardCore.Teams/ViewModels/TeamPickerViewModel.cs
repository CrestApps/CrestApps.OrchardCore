namespace CrestApps.OrchardCore.Teams.ViewModels;

/// <summary>
/// The model of the team picker.
/// </summary>
public sealed class TeamPickerViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the select element.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the chosen team ids are posted under.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets whether several teams can be chosen.
    /// </summary>
    public bool Multiple { get; set; }

    /// <summary>
    /// Gets or sets whether a choice is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the text shown while nothing is chosen, or <see langword="null"/> for the default.
    /// </summary>
    public string ButtonText { get; set; }

    /// <summary>
    /// Gets or sets whether the button is small.
    /// </summary>
    public bool SmallButton { get; set; }

    /// <summary>
    /// Gets or sets the teams offered, parents before their children.
    /// </summary>
    public IReadOnlyList<TeamPickerOption> Options { get; set; } = [];
}
