namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The details of a survey staff edit: title, name, description and tags.
/// </summary>
public class SurveyDetailsEditViewModel
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the notes staff see.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the tags, separated by commas.
    /// </summary>
    public string Tags { get; set; }
}
