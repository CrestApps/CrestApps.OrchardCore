using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page of one response for staff.
/// </summary>
public class SurveyResponseDetailsViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public SurveyVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the name of the respondent, or <see langword="null"/> when anonymous.
    /// </summary>
    public string RespondentName { get; set; }

    /// <summary>
    /// Gets or sets the label staff see for the response: its number, or a keyed code on an anonymous survey.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets whether the response's version number may show. On an anonymous survey, a version too few completed
    /// responses share is not shown.
    /// </summary>
    public bool ShowsVersion { get; set; } = true;

    /// <summary>
    /// Gets or sets the question shapes with all details, page by page.
    /// </summary>
    public IList<IShape> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the display of the response that other features extend (such as the grading form).
    /// </summary>
    public IShape Extensions { get; set; }

    /// <summary>
    /// Gets or sets the page names by page id, for the page visits.
    /// </summary>
    public IDictionary<string, string> PageNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the category titles by category id.
    /// </summary>
    public IDictionary<string, string> CategoryNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether the viewer may void and delete the response.
    /// </summary>
    public bool CanDelete { get; set; }
}
