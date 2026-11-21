namespace CrestApps.OrchardCore.Learning.Assessments.Models;

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
/// When a learner gets feedback on an answer.
/// </summary>
public enum QuizFeedbackMode
{
    /// <summary>After the attempt is submitted, as the review options allow.</summary>
    Deferred,

    /// <summary>Per question during the attempt: the learner checks an answer, which then cannot change.</summary>
    Immediate,
}

/// <summary>
/// What a learner sees of an attempt at a moment of the review.
/// </summary>
[Flags]
public enum ReviewShows
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The scores of the attempt and of each question.</summary>
    Score = 1,

    /// <summary>The right answers.</summary>
    RightAnswers = 2,

    /// <summary>The feedback of the answers chosen and the comments of the grader.</summary>
    Feedback = 4,

    /// <summary>The general feedback of each question.</summary>
    GeneralFeedback = 8,

    /// <summary>Everything.</summary>
    All = Score | RightAnswers | Feedback | GeneralFeedback,
}

/// <summary>
/// The moments of the review, as Moodle names them.
/// </summary>
public enum ReviewMoment
{
    /// <summary>During the attempt, for questions checked with immediate feedback.</summary>
    During,

    /// <summary>Within two minutes of the submit.</summary>
    ImmediatelyAfter,

    /// <summary>Later, while the quiz is still open.</summary>
    LaterWhileOpen,

    /// <summary>After the quiz closes.</summary>
    AfterClose,
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
