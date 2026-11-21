using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Computes the course grade and whether it meets the course pass mark. An item counts when it is gradable and its
/// weight is more than zero; an item without a score counts as zero.
/// <list type="bullet">
/// <item>A course without gradebook categories: the grade is the weighted mean (item weights) of the items.</item>
/// <item>A course with categories: the score of a category is the weighted mean (item weights) of its items after its
/// <see cref="GradeCategory.DropLowest"/> lowest scores are left out (an item without a score before a real zero; at
/// least one item always counts). The grade is the weighted mean (category weights) of the categories that have an item
/// that counts.</item>
/// <item>Items without a category, or with a category the course no longer has, count as one more category whose weight
/// is the sum of their item weights and which drops nothing. So each of them weighs in the grade as if it were a
/// category of its own with its item weight, which is also exactly how a course without categories works.</item>
/// </list>
/// </summary>
public static class CourseGradeCalculator
{
    /// <summary>
    /// Computes the course grade.
    /// </summary>
    /// <param name="outline">The outline.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>The grade as a percentage rounded to two decimals, or <see langword="null"/> when no item counts.</returns>
    public static decimal? Compute(CourseOutline outline, EnrollmentProgress progress)
        => ComputeBreakdown(outline, progress).Grade;

    /// <summary>
    /// Computes the course grade with the score of each gradebook category and the items each one left out.
    /// </summary>
    /// <param name="outline">The outline.</param>
    /// <param name="progress">The progress of the enrollment, or <see langword="null"/> when there is none.</param>
    /// <returns>The breakdown; its categories are empty when the course has none.</returns>
    public static CourseGradeBreakdown ComputeBreakdown(CourseOutline outline, EnrollmentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var gradable = outline.Entries.Where(entry => entry.Part?.Gradable == true).ToList();
        var categories = (outline.Course?.Get<CoursePart>(nameof(CoursePart))?.GradeCategories ?? [])
            .Where(category => !string.IsNullOrEmpty(category?.Id))
            .ToList();

        if (categories.Count == 0)
        {
            var (weights, total) = Sum(gradable, progress);

            return new CourseGradeBreakdown { Grade = weights == 0 ? null : Round(total / weights) };
        }

        var known = categories.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        var results = new List<GradeCategoryResult>();
        decimal courseWeights = 0;
        decimal courseTotal = 0;

        foreach (var category in categories)
        {
            var entries = gradable.Where(entry => string.Equals(entry.Part.GradeCategoryId, category.Id, StringComparison.Ordinal)).ToList();
            var counting = entries
                .Where(entry => entry.Part.Weight > 0)
                .Select((entry, index) => (Entry: entry, Score: progress?.Find(entry.Item.ContentItemId)?.Score, Index: index))
                .ToList();
            var dropCount = Math.Min(Math.Max(category.DropLowest, 0), Math.Max(counting.Count - 1, 0));
            var dropped = counting
                .OrderBy(candidate => candidate.Score ?? 0)
                .ThenBy(candidate => candidate.Score.HasValue ? 1 : 0)
                .ThenBy(candidate => candidate.Index)
                .Take(dropCount)
                .Select(candidate => candidate.Entry.Item.ContentItemId)
                .ToHashSet(StringComparer.Ordinal);
            var (weights, total) = Sum(counting.Select(candidate => candidate.Entry).Where(entry => !dropped.Contains(entry.Item.ContentItemId)), progress);
            decimal? score = weights == 0 ? null : total / weights;

            if (score is { } value && category.Weight > 0)
            {
                courseWeights += category.Weight;
                courseTotal += category.Weight * value;
            }

            results.Add(new GradeCategoryResult
            {
                CategoryId = category.Id,
                Name = category.Name,
                Weight = category.Weight,
                Score = score is { } shown ? Round(shown) : null,
                ItemIds = entries.Select(entry => entry.Item.ContentItemId).ToList(),
                DroppedItemIds = entries.Select(entry => entry.Item.ContentItemId).Where(dropped.Contains).ToList(),
            });
        }

        var others = gradable.Where(entry => string.IsNullOrEmpty(entry.Part.GradeCategoryId) || !known.Contains(entry.Part.GradeCategoryId)).ToList();

        if (others.Count > 0)
        {
            var (weights, total) = Sum(others, progress);

            // Each item without a category weighs in the grade with its own item weight.
            courseWeights += weights;
            courseTotal += total;

            results.Add(new GradeCategoryResult
            {
                Weight = weights,
                Score = weights == 0 ? null : Round(total / weights),
                ItemIds = others.Select(entry => entry.Item.ContentItemId).ToList(),
            });
        }

        return new CourseGradeBreakdown
        {
            Grade = courseWeights == 0 ? null : Round(courseTotal / courseWeights),
            Categories = results,
        };
    }

    /// <summary>
    /// Tells whether a grade meets a pass mark.
    /// </summary>
    /// <param name="grade">The grade.</param>
    /// <param name="passMark">The pass mark.</param>
    /// <returns><see langword="null"/> when there is no grade or no pass mark.</returns>
    public static bool? IsPassed(decimal? grade, decimal? passMark)
        => grade is null || passMark is null ? null : grade >= passMark;

    private static (decimal Weights, decimal Total) Sum(IEnumerable<CourseOutlineEntry> entries, EnrollmentProgress progress)
    {
        decimal weights = 0;
        decimal total = 0;

        foreach (var entry in entries)
        {
            var weight = entry.Part.Weight <= 0 ? 0 : entry.Part.Weight;

            weights += weight;
            total += weight * (progress?.Find(entry.Item.ContentItemId)?.Score ?? 0);
        }

        return (weights, total);
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
