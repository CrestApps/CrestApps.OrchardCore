namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The settings of the course item part on a content type: which kind its items are and how they are completed by
/// default. The kind is fixed by the type, so an author never changes it on an item.
/// </summary>
public sealed class CourseItemPartSettings
{
    /// <summary>
    /// Gets or sets the kind of the type's items, such as <c>Page</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the completion mode a new item starts with.
    /// </summary>
    public string DefaultCompletionMode { get; set; }

    /// <summary>
    /// Gets or sets whether a new item starts as gradable, so its score counts towards the course grade. The author can
    /// still change it on the item.
    /// </summary>
    public bool Gradable { get; set; }
}
