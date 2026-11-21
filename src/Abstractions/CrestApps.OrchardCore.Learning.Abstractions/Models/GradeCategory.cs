namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A gradebook category of a course, such as <c>Homework</c> or <c>Exams</c>: the gradable items in it are averaged
/// together, after their lowest scores are dropped, and the category counts in the course grade with its own weight.
/// </summary>
public sealed class GradeCategory
{
    /// <summary>
    /// Gets or sets the id, generated when the category is added and kept while it exists. Course items name it in
    /// <see cref="CourseItemPart.GradeCategoryId"/>.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name shown in the editors and the gradebook.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the weight of the category in the course grade; more than zero.
    /// </summary>
    public decimal Weight { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many of the lowest scores of the category are left out of it. At least one item of the
    /// category always counts.
    /// </summary>
    public int DropLowest { get; set; }
}
