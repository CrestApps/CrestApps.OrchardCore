namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Describes a survey template in the new survey page.
/// </summary>
public sealed class SurveyTemplateDescriptor
{
    /// <summary>
    /// Gets or sets the unique name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description shown.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of survey the template makes.
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets the Font Awesome class of its icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-clipboard-list";

    /// <summary>
    /// Gets or sets its position in the list.
    /// </summary>
    public int Position { get; set; }
}
