namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One question on the grading page.
/// </summary>
public class GradeQuestionViewModel
{
    /// <summary>
    /// Gets or sets the question id.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question text.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the answer as text.
    /// </summary>
    public string Answer { get; set; }

    /// <summary>
    /// Gets or sets the points given (the grader's, else the automatic ones).
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the most points.
    /// </summary>
    public decimal MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets whether the question waits for a grader.
    /// </summary>
    public bool NeedsGrading { get; set; }

    /// <summary>
    /// Gets or sets the grader's comment.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets the guidance for graders.
    /// </summary>
    public string GraderGuidance { get; set; }

    /// <summary>
    /// Gets or sets whether the grader used AI help.
    /// </summary>
    public bool AIAssisted { get; set; }
}
