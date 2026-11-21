using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Which questions a quiz's "Draw at random" rule may draw: those with the rule's tag, whatever its case, and its
/// difficulty, each only when the rule sets it. An attempt draws by it, and the quiz editor counts what a bank can
/// supply to a rule by it, so the two always agree.
/// </summary>
public static class QuizDrawRuleMatcher
{
    /// <summary>
    /// Returns whether a question may be drawn by a rule.
    /// </summary>
    /// <param name="question">The question's part, or <see langword="null"/> for a question without one.</param>
    /// <param name="rule">The rule.</param>
    public static bool Matches(QuestionPart question, QuizDrawRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return (string.IsNullOrEmpty(rule.Tag) || (question?.Tags?.Contains(rule.Tag, StringComparer.OrdinalIgnoreCase) ?? false))
            && (rule.Difficulty is null || question?.Difficulty == rule.Difficulty);
    }
}
