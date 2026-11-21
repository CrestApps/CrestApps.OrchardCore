using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// What a learner sees of a quiz: their attempts and whether they may start one.
/// </summary>
public class QuizPanelViewModel
{
    /// <summary>
    /// Gets or sets the id of the quiz course item.
    /// </summary>
    public string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/>.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the definition of the published version, or <see langword="null"/>.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets whether the person is signed in.
    /// </summary>
    public bool SignedIn { get; set; }

    /// <summary>
    /// Gets or sets whether the person may attempt the quiz (enrolled and released).
    /// </summary>
    public bool Enrolled { get; set; }

    /// <summary>
    /// Gets or sets the learner's attempts, newest first.
    /// </summary>
    public IReadOnlyList<SurveyResponse> Attempts { get; set; } = [];

    /// <summary>
    /// Gets or sets the attempt in progress, or <see langword="null"/>.
    /// </summary>
    public SurveyResponse Open { get; set; }

    /// <summary>
    /// Gets or sets how many attempts are left, or <see langword="null"/> for no limit.
    /// </summary>
    public int? AttemptsLeft { get; set; }
}
