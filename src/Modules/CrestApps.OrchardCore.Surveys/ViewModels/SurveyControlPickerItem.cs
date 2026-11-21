namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One entry of the add question picker: a question type or a preset.
/// </summary>
public class SurveyControlPickerItem
{
    /// <summary>
    /// Gets or sets the question type.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the preset, or <see langword="null"/>.
    /// </summary>
    public string Preset { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the icon class.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }
}
