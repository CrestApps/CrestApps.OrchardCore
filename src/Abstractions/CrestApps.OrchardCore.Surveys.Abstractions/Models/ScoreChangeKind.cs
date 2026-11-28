namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A kind of score change.
/// </summary>
public enum ScoreChangeKind
{
    /// <summary>
    /// Scored when completed.
    /// </summary>
    AutoScored,

    /// <summary>
    /// A grader scored a question.
    /// </summary>
    Graded,

    /// <summary>
    /// The response was scored again.
    /// </summary>
    Regraded,

    /// <summary>
    /// Staff changed the score.
    /// </summary>
    Changed,

    /// <summary>
    /// The response was voided.
    /// </summary>
    Voided,
}
