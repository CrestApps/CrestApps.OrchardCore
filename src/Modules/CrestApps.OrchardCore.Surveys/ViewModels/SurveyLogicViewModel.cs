using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The logic overview page.
/// </summary>
public class SurveyLogicViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the logic of each page.
    /// </summary>
    public List<SurveyLogicPageViewModel> Pages { get; set; } = [];

    /// <summary>
    /// Gets or sets the endings in plain words.
    /// </summary>
    public List<string> Endings { get; set; } = [];

    /// <summary>
    /// Gets or sets the problems the validator found.
    /// </summary>
    public IReadOnlyList<SurveyValidationMessage> Problems { get; set; } = [];
}
