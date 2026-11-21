using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Turns a learner's SCORM 1.2 or SCORM 2004 data into the result Learning records: the score as a percentage, passed or failed, and
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
    /// Gets the SCORM 2004 score as a percentage: <c>score.scaled</c> times 100 when the content set it, else the raw
    /// score within <c>score.min</c> and <c>score.max</c> when it set a range (the raw score otherwise), kept within 0 to
    /// 100 and rounded to two decimals.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns>The percentage, or <see langword="null"/> without a scaled or raw score.</returns>
    public static decimal? ScorePercent(Scorm2004Cmi cmi)
    {
        if (cmi is null)
        {
            return null;
        }

        decimal percent;

        if (Scorm2004DataModel.TryParseReal(cmi.ScoreScaled, out var scaled))
        {
            percent = scaled * 100;
        }
        else if (Scorm2004DataModel.TryParseReal(cmi.ScoreRaw, out var raw))
        {
            var min = Scorm2004DataModel.TryParseReal(cmi.ScoreMin, out var low) ? low : 0;
            var hasMax = Scorm2004DataModel.TryParseReal(cmi.ScoreMax, out var max);

            percent = hasMax && max > min ? (raw - min) * 100 / (max - min) : raw;
        }
        else
        {
            return null;
        }

        return Math.Round(Math.Clamp(percent, 0, 100), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Builds the result of a SCORM 2004 package item from the evaluated statuses (RTE 4.2.4.1 and 4.2.22.1): complete
    /// when <c>cmi.completion_status</c> reads <c>completed</c>, passed or failed from <c>cmi.success_status</c>.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="itemId">The package item.</param>
    /// <param name="attempts">The attempts so far.</param>
    /// <returns>The result, marked self-reported.</returns>
    public static CourseItemResult Map(Scorm2004Cmi cmi, string itemId, int attempts)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        return new CourseItemResult
        {
            ItemId = itemId,
            Score = ScorePercent(cmi),
            Passed = Scorm2004DataModel.EvaluateSuccessStatus(cmi) switch
            {
                "passed" => true,
                "failed" => false,
                _ => null,
            },
            ReportedComplete = Scorm2004DataModel.EvaluateCompletionStatus(cmi) == "completed",
            Attempts = attempts,

            // The browser reported it: Learning keeps the mark on the item's progress and in the completion snapshot.
            SelfReported = true,
        };
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
