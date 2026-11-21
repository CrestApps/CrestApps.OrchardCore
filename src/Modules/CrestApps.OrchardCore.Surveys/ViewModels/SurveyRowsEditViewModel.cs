using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The rows of a question.
/// </summary>
public class SurveyRowsEditViewModel
{
    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public List<SurveySubQuestion> Rows { get; set; } = [];
}
