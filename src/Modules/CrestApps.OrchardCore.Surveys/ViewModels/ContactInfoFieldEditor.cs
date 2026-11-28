namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One field of the contact information editor.
/// </summary>
public class ContactInfoFieldEditor
{
    /// <summary>
    /// Gets or sets the field kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets whether the field is asked for: none, optional or required.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets the label, or <see langword="null"/> for the default.
    /// </summary>
    public string Label { get; set; }
}
