using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The designer page.
/// </summary>
public class SurveyDesignerViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the outline.
    /// </summary>
    public SurveyOutlineViewModel Outline { get; set; }

    /// <summary>
    /// Gets or sets the editor of the survey-wide settings.
    /// </summary>
    public IShape SettingsEditor { get; set; }

    /// <summary>
    /// Gets or sets the add question picker.
    /// </summary>
    public IReadOnlyList<SurveyControlPickerItem> Picker { get; set; } = [];

    /// <summary>
    /// Gets or sets when the draft was loaded, as ticks, so a save by someone else is noticed.
    /// </summary>
    public long DraftVersion { get; set; }
}
