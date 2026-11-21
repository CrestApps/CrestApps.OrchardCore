namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A course grade with the score of each gradebook category and the items each one left out.
/// </summary>
public sealed class CourseGradeBreakdown
{
    /// <summary>
    /// Gets or sets the course grade as a percentage rounded to two decimals, or <see langword="null"/> when no item
    /// counts.
    /// </summary>
    public decimal? Grade { get; set; }

    /// <summary>
    /// Gets or sets the result of each category, in the order of the course's categories, with the items that have no
    /// category last. Empty when the course has no categories.
    /// </summary>
    public IReadOnlyList<GradeCategoryResult> Categories { get; set; } = [];

    /// <summary>
    /// Gets whether an item was left out of its category as one of its lowest scores.
    /// </summary>
    /// <param name="contentItemId">The content item id of the course item.</param>
    /// <returns><see langword="true"/> when the item was dropped.</returns>
    public bool IsDropped(string contentItemId)
        => Categories.Any(category => category.DroppedItemIds.Contains(contentItemId, StringComparer.Ordinal));
}

/// <summary>
/// The result of one gradebook category.
/// </summary>
public sealed class GradeCategoryResult
{
    /// <summary>
    /// Gets or sets the id of the category, or <see langword="null"/> for the items that have no category.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the category, or <see langword="null"/> for the items that have no category.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the weight the category counts with in the course grade. For the items without a category it is
    /// the sum of their item weights.
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Gets or sets the score of the category as a percentage rounded to two decimals, or <see langword="null"/> when
    /// none of its items counts.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the content item ids of the gradable items of the category, in outline order.
    /// </summary>
    public IReadOnlyList<string> ItemIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the content item ids of the items left out as the lowest scores.
    /// </summary>
    public IReadOnlyList<string> DroppedItemIds { get; set; } = [];
}
