namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// The model of the picker the <see cref="PartyPickerViewComponent"/> renders.
/// </summary>
public sealed class PartyPickerViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the picker element.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the chosen party references are posted under.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets the kinds offered, comma-separated, or empty for every kind.
    /// </summary>
    public string Kinds { get; set; }

    /// <summary>
    /// Gets or sets whether several parties can be chosen.
    /// </summary>
    public bool Multiple { get; set; }

    /// <summary>
    /// Gets or sets the button text, or <see langword="null"/> for the default.
    /// </summary>
    public string ButtonText { get; set; }

    /// <summary>
    /// Gets or sets whether the button is small.
    /// </summary>
    public bool SmallButton { get; set; }

    /// <summary>
    /// Gets or sets whether a choice is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the parties chosen now, as the item selector's JSON.
    /// </summary>
    public string InitialItemsJson { get; set; }
}
