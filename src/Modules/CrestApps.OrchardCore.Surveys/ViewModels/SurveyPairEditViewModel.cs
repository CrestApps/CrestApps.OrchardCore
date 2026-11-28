namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The right answer of one row of a matching question.
/// </summary>
public class SurveyPairEditViewModel
{
    /// <summary>
    /// Gets or sets the row id.
    /// </summary>
    public string RowId { get; set; }

    /// <summary>
    /// Gets or sets the row text.
    /// </summary>
    public string RowText { get; set; }

    /// <summary>
    /// Gets or sets the id of the right choice.
    /// </summary>
    public string ChoiceId { get; set; }
}
