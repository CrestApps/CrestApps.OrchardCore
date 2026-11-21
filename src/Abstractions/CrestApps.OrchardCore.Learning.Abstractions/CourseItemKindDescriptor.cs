using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Describes a course item kind: its names, the completion modes it supports, whether it produces a grade and the shape
/// the player renders it with. Registered with <c>AddCourseItemKind</c>.
/// </summary>
public sealed class CourseItemKindDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CourseItemKindDescriptor"/> class.
    /// </summary>
    /// <param name="name">The stable name of the kind.</param>
    public CourseItemKindDescriptor(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }

    /// <summary>
    /// Gets the stable name of the kind, stored on <c>CourseItemPart.Kind</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the name shown to authors.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the name shown to learners, such as "Lesson".
    /// </summary>
    [JsonIgnore]
    public LocalizationSource LearnerName { get; set; }

    /// <summary>
    /// Gets or sets the name of the content part that marks the kind, when it has one.
    /// </summary>
    public string PartName { get; set; }

    /// <summary>
    /// Gets or sets the content type Learning creates for the kind, when it creates one.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets the completion modes the kind supports. The first one is the default.
    /// </summary>
    public IList<string> CompletionModes { get; } = [];

    /// <summary>
    /// Gets or sets whether the kind produces a score.
    /// </summary>
    public bool ProducesGrade { get; set; }

    /// <summary>
    /// Gets or sets the name of the shape the player renders the item with. Empty means the item's own display.
    /// </summary>
    public string PlayerShape { get; set; }

    /// <summary>
    /// Gets or sets the icon class shown beside the item in the outline.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-file-lines";

    /// <summary>
    /// Gets or sets whether items of this kind are shown in place, in the outline and in the player, instead of being
    /// opened on their own. Such an item is never required, never tracked for completion and never counted in the
    /// progress.
    /// </summary>
    public bool Inline { get; set; }

    /// <summary>
    /// Gets the default completion mode.
    /// </summary>
    public string DefaultCompletionMode => CompletionModes.Count > 0 ? CompletionModes[0] : Models.CompletionModes.Viewed;

    /// <summary>
    /// Gets or sets whether a gradable item of this kind completes only when it is graded. Work that a person grades after
    /// it is handed in would otherwise complete the course before its grade exists.
    /// </summary>
    public bool GradableCompletesWhenGraded { get; set; }

    /// <summary>
    /// Gets whether an item of this kind can use a completion mode.
    /// </summary>
    /// <param name="mode">The completion mode.</param>
    /// <param name="gradable">Whether the item counts toward the course grade.</param>
    /// <returns><see langword="true"/> when the kind supports the mode and, for a gradable item of a kind graded by a person, the mode is Graded.</returns>
    public bool Allows(string mode, bool gradable)
        => Supports(mode) && (!gradable || !GradableCompletesWhenGraded || mode == Models.CompletionModes.Graded);

    /// <summary>
    /// Tells whether the kind supports a completion mode.
    /// </summary>
    /// <param name="mode">The completion mode.</param>
    /// <returns><see langword="true"/> when the kind supports it.</returns>
    public bool Supports(string mode)
        => !string.IsNullOrEmpty(mode) && CompletionModes.Contains(mode, StringComparer.Ordinal);
}
