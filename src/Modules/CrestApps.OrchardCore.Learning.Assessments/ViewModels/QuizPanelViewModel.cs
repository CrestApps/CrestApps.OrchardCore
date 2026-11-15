using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The quiz panel a learner sees on the quiz item.
/// </summary>
public class QuizPanelViewModel
{
    /// <summary>Gets or sets the quiz.</summary>
    public string QuizId { get; set; }

    /// <summary>Gets or sets the quiz part.</summary>
    public QuizPart Quiz { get; set; }

    /// <summary>Gets or sets whether the person is signed in.</summary>
    public bool SignedIn { get; set; }

    /// <summary>Gets or sets the attempts of the learner, newest first.</summary>
    public IList<AssessmentAttempt> Attempts { get; set; } = [];

    /// <summary>Gets or sets the attempt in progress.</summary>
    public AssessmentAttempt Open { get; set; }

    /// <summary>Gets or sets the attempts left, or <see langword="null"/> for no limit.</summary>
    public int? AttemptsLeft { get; set; }

    /// <summary>Gets or sets the number of questions when fixed and drawn.</summary>
    public int QuestionCount { get; set; }

    /// <summary>
    /// Gets or sets whether the learner must type an access code to start.
    /// </summary>
    public bool NeedsAccessCode { get; set; }
}
