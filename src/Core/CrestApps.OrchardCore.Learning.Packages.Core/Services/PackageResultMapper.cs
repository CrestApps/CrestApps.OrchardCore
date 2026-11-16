using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Turns a learner's SCORM 1.2 data into the result Learning records: the score as a percentage, passed or failed, and
/// whether the package reported the item complete. The result is always marked self-reported: the browser reported it.
/// </summary>
public static class PackageResultMapper
{
    /// <summary>
    /// Gets the score as a percentage: the raw score within <c>score.min</c> and <c>score.max</c> when the content set a
    /// range (0 to 100 otherwise), rounded to two decimals.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns>The percentage, or <see langword="null"/> without a raw score.</returns>
    public static decimal? ScorePercent(Scorm12Cmi cmi)
    {
        if (cmi is null || !Scorm12Rules.TryParseNumber(cmi.ScoreRaw, out var raw))
        {
            return null;
        }

        var min = Scorm12Rules.TryParseNumber(cmi.ScoreMin, out var low) ? low : 0;
        var hasMax = Scorm12Rules.TryParseNumber(cmi.ScoreMax, out var max);

        // A range the content set scales the raw score; without one (or with an empty range) the raw score is the
        // percentage, as SCORM 1.2 scores are from 0 to 100.
        var percent = hasMax && max > min ? (raw - min) * 100 / (max - min) : raw;

        return Math.Round(Math.Clamp(percent, 0, 100), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Builds the result of a package item.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="itemId">The package item.</param>
    /// <param name="attempts">The sessions so far.</param>
    /// <returns>The result, marked self-reported.</returns>
    public static CourseItemResult Map(Scorm12Cmi cmi, string itemId, int attempts)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        var status = cmi.LessonStatus;

        return new CourseItemResult
        {
            ItemId = itemId,
            Score = ScorePercent(cmi),
            Passed = status switch
            {
                "passed" => true,
                "failed" => false,
                _ => null,
            },
            ReportedComplete = status is "completed" or "passed",
            Attempts = attempts,

            // The browser reported it: Learning keeps the mark on the item's progress and in the completion snapshot.
            SelfReported = true,
        };
    }
}
