using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// The attempts of a learning package with attempt settings (a limit, a grading method), as Moodle runs SCORM: a launch
/// after a finished attempt starts a new one until the limit, then only reviews, and the item's score comes from the
/// scores of the attempts by the grading method. A package without attempt settings keeps one attempt that resumes.
/// </summary>
public static class PackageAttempts
{
    /// <summary>
    /// Tells whether a package counts attempts.
    /// </summary>
    /// <param name="part">The package.</param>
    /// <returns><see langword="true"/> with a limit or a grading method.</returns>
    public static bool Tracks(LearningPackagePart part)
        => part is not null && (part.MaxAttempts is > 0 || part.GradingMethod is not null);

    /// <summary>
    /// Tells whether the next launch starts a new attempt: the first launch, a SCORM 2004 launch that does not resume a
    /// suspended session (RTE 4.2.8), or a SCORM 1.2 launch after an attempt that reported completed, passed or failed and
    /// was not suspended.
    /// </summary>
    /// <param name="state">The learner's state on the package, or <see langword="null"/> before the first launch.</param>
    /// <param name="scorm2004">Whether the package is SCORM 2004.</param>
    /// <returns><see langword="true"/> when a new attempt starts.</returns>
    public static bool StartsNewAttempt(PackageRuntimeState state, bool scorm2004)
    {
        if (state is null || state.Sessions == 0)
        {
            return true;
        }

        if (scorm2004)
        {
            return !string.Equals(state.Cmi2004?.Exit, "suspend", StringComparison.Ordinal);
        }

        var cmi = state.Cmi;

        return cmi is not null &&
            !string.Equals(cmi.Exit, "suspend", StringComparison.Ordinal) &&
            cmi.LessonStatus is "completed" or "passed" or "failed";
    }

    /// <summary>
    /// Tells whether the learner has used every attempt the package allows.
    /// </summary>
    /// <param name="part">The package.</param>
    /// <param name="state">The learner's state on the package.</param>
    /// <returns><see langword="true"/> when no attempt is left.</returns>
    public static bool UsedUp(LearningPackagePart part, PackageRuntimeState state)
        => part?.MaxAttempts is int max && max > 0 && (state?.Attempts ?? 0) >= max;

    /// <summary>
    /// Keeps the result of the attempt in progress and, with a grading method, gives the item the score of its attempts.
    /// The item completes or passes when any attempt did.
    /// </summary>
    /// <param name="state">The learner's state on the package.</param>
    /// <param name="part">The package.</param>
    /// <param name="result">The result of the attempt in progress.</param>
    /// <returns>The result to record on the item.</returns>
    public static CourseItemResult Apply(PackageRuntimeState state, LearningPackagePart part, CourseItemResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        if (!Tracks(part))
        {
            return result;
        }

        var number = Math.Max(state.Attempts, 1);
        var attempts = state.AttemptScores ??= [];
        var current = attempts.FirstOrDefault(attempt => attempt.Number == number);

        if (current is null)
        {
            current = new PackageAttemptScore { Number = number };
            attempts.Add(current);
        }

        current.Score = result.Score;
        current.Passed = result.Passed;
        current.Completed = result.ReportedComplete;

        var ordered = attempts.OrderBy(attempt => attempt.Number).ToList();

        if (part.GradingMethod is { } method)
        {
            result.Score = AttemptScores.Combine([.. ordered.Where(attempt => attempt.Score is not null).Select(attempt => attempt.Score.Value)], method);
        }

        if (ordered.Any(attempt => attempt.Passed == true))
        {
            result.Passed = true;
        }

        result.ReportedComplete = ordered.Any(attempt => attempt.Completed);
        result.Attempts = number;

        return result;
    }
}
