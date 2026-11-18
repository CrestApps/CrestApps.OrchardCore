namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// The model of the picker the <see cref="RecordPickerViewComponent"/> renders.
/// </summary>
public sealed class RecordPickerViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the picker element.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the chosen values are posted under.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets the kind of record offered.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the name of the kind, shown in the picker's texts.
    /// </summary>
    public string KindDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the form of the posted values, one of <c>RecordPickerValueTypes</c>.
    /// </summary>
    public string ValueType { get; set; }

    /// <summary>
    /// Gets or sets whether several records can be chosen.
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
    /// Gets or sets the records chosen now, as the item selector's JSON.
    /// </summary>
    public string InitialItemsJson { get; set; }
}
