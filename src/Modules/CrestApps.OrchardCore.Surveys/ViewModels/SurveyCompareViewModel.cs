using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The comparison of two versions, or of a version and the draft.
/// </summary>
public class SurveyCompareViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the label of the older side.
    /// </summary>
    public string FromLabel { get; set; }

    /// <summary>
    /// Gets or sets the label of the newer side.
    /// </summary>
    public string ToLabel { get; set; }

    /// <summary>
    /// Gets or sets the changes.
    /// </summary>
    public SurveyDefinitionDiff Diff { get; set; }
}
