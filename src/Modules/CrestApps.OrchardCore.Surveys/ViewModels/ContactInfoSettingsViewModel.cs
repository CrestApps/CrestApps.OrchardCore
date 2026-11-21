namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The editor of contact information questions: each field and whether it is asked for.
/// </summary>
public class ContactInfoSettingsViewModel
{
    /// <summary>
    /// Gets or sets the fields, one per kind.
    /// </summary>
    public List<ContactInfoFieldEditor> Fields { get; set; } = [];
}
