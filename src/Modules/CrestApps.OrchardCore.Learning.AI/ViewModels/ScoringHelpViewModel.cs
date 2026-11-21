namespace CrestApps.OrchardCore.Learning.AI.ViewModels;

/// <summary>
/// The scoring help panel of a grading page.
/// </summary>
public class ScoringHelpViewModel
{
    /// <summary>
    /// Gets or sets the address that returns the suggestion.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets whether an AI deployment is set up.
    /// </summary>
    public bool ModelAvailable { get; set; }

    /// <summary>
    /// Gets or sets the panel's element id.
    /// </summary>
    public string HtmlId { get; set; }

    /// <summary>
    /// Gets or sets the form field that holds the points.
    /// </summary>
    public string PointsField { get; set; }

    /// <summary>
    /// Gets or sets the form field that holds the feedback.
    /// </summary>
    public string FeedbackField { get; set; }

    /// <summary>
    /// Gets or sets the start of the names of the rubric's form fields.
    /// </summary>
    public string RubricFieldPrefix { get; set; }

    /// <summary>
    /// Gets or sets the form field that records the grader used the help.
    /// </summary>
    public string AssistedField { get; set; }
}
