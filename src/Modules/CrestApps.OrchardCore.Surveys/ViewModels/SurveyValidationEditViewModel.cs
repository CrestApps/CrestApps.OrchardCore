using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The validation rules of a question.
/// </summary>
public class SurveyValidationEditViewModel
{
    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public List<SurveyValidationRule> ValidationRules { get; set; } = [];
}
