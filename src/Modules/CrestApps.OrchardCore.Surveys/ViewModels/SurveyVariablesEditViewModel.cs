using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The variables of a survey definition.
/// </summary>
public class SurveyVariablesEditViewModel
{
    /// <summary>
    /// Gets or sets the variables.
    /// </summary>
    public List<SurveyVariable> Variables { get; set; } = [];
}
