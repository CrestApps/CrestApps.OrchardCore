using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The outline of a draft: pages and their questions.
/// </summary>
public class SurveyOutlineViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the registered question types by name.
    /// </summary>
    public IReadOnlyDictionary<string, SurveyControlDescriptor> Controls { get; set; } = new Dictionary<string, SurveyControlDescriptor>();

    /// <summary>
    /// Gets or sets the names shown of the question types, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ControlNames { get; set; } = new Dictionary<string, string>();
}
