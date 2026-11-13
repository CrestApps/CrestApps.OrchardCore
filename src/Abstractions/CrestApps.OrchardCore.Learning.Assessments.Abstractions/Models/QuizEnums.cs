namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// Which attempt sets the quiz score.
/// </summary>
public enum GradingMethod
{
    /// <summary>The best attempt.</summary>
    Highest,

    /// <summary>The last graded attempt.</summary>
    Latest,

    /// <summary>The mean of the graded attempts.</summary>
    Average,

    /// <summary>The first graded attempt.</summary>
    First,
}

/// <summary>
/// When a learner may see the right answers and the feedback.
/// </summary>
public enum ReviewPolicy
{
    /// <summary>Never; the learner sees only the score.</summary>
    ScoreOnly,

    /// <summary>When the attempt is submitted and graded.</summary>
    AfterGraded,

    /// <summary>When the quiz closes for everyone.</summary>
    AfterClose,

    /// <summary>Never, not even the score.</summary>
    Hidden,
}

/// <summary>
/// How a learner moves through the questions.
/// </summary>
public enum QuizNavigation
{
    /// <summary>In any order, coming back to any question.</summary>
    Free,

    /// <summary>One after another, without going back.</summary>
    Sequential,
}

/// <summary>
/// The states of an attempt.
/// </summary>
public enum AttemptStatus
{
    /// <summary>The learner is answering.</summary>
    InProgress,

    /// <summary>Submitted and scored automatically, with nothing left for a grader.</summary>
    Submitted,

    /// <summary>Submitted, with answers a grader must mark.</summary>
    NeedsGrading,

    /// <summary>Every answer is scored.</summary>
    Graded,

    /// <summary>Left without being submitted, such as when the quiz closed.</summary>
    Abandoned,

    /// <summary>Cancelled by staff with a reason; it does not count.</summary>
    Voided,
}
