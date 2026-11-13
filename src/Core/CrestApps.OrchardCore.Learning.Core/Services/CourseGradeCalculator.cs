using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Computes the course grade: the weighted mean of the scores of the gradable items in the outline, where an item
/// without a score counts as zero, and whether it meets the course pass mark.
/// </summary>
public static class CourseGradeCalculator
{
    /// <summary>
    /// Computes the course grade.
    /// </summary>
    /// <param name="outline">The outline.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>The grade as a percentage rounded to two decimals, or <see langword="null"/> when no item is gradable.</returns>
    public static decimal? Compute(CourseOutline outline, EnrollmentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(outline);

        decimal weights = 0;
        decimal total = 0;

        foreach (var entry in outline.Entries.Where(entry => entry.Part?.Gradable == true))
        {
            var weight = entry.Part.Weight <= 0 ? 0 : entry.Part.Weight;

            weights += weight;
            total += weight * (progress?.Find(entry.Item.ContentItemId)?.Score ?? 0);
        }

        if (weights == 0)
        {
            return null;
        }

        return Math.Round(total / weights, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Tells whether a grade meets a pass mark.
    /// </summary>
    /// <param name="grade">The grade.</param>
    /// <param name="passMark">The pass mark.</param>
    /// <returns><see langword="null"/> when there is no grade or no pass mark.</returns>
    public static bool? IsPassed(decimal? grade, decimal? passMark)
        => grade is null || passMark is null ? null : grade >= passMark;
}
