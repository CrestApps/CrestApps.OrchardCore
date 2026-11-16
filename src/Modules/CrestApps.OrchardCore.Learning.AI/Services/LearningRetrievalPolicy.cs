using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// What the retrieval filter knows about one retrieved chunk's content item.
/// </summary>
internal sealed class LearningRetrievalCandidate
{
    /// <summary>
    /// Gets whether the chunk refers to a published content item.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets whether the item is exam content: a question, a question bank or a quiz.
    /// </summary>
    public bool IsExamContent { get; init; }

    /// <summary>
    /// Gets whether the item is a course item.
    /// </summary>
    public bool IsCourseItem { get; init; }

    /// <summary>
    /// Gets the course item kind.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets whether the learning assistant of the item's course is on.
    /// </summary>
    public bool AssistantEnabled { get; init; }

    /// <summary>
    /// Gets the course access decision for the person.
    /// </summary>
    public CourseItemAccessReason Access { get; init; } = CourseItemAccessReason.NotACourseItem;

    /// <summary>
    /// Gets whether the person may view the item.
    /// </summary>
    public bool CanView { get; init; }
}

/// <summary>
/// Decides which retrieved chunks reach a model. Exam content never does. A tutor gets only lessons of the courses the
/// learner is actively enrolled in, within the access dates and release rules, whose learning assistant is on. Any
/// other AI profile gets course items only when the person may view them, and everything else as before.
/// </summary>
internal static class LearningRetrievalPolicy
{
    /// <summary>
    /// Gets whether a chunk may reach the model.
    /// </summary>
    /// <param name="candidate">What is known about the chunk's content item.</param>
    /// <param name="isTutor">Whether the AI profile is a learning tutor.</param>
    /// <param name="lessonKinds">The course item kinds a tutor may answer from.</param>
    /// <returns><see langword="true"/> to keep the chunk.</returns>
    public static bool Keep(LearningRetrievalCandidate candidate, bool isTutor, IReadOnlyCollection<string> lessonKinds)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.IsExamContent)
        {
            return false;
        }

        if (isTutor)
        {
            return candidate.Found
                && candidate.IsCourseItem
                && !string.IsNullOrEmpty(candidate.Kind)
                && lessonKinds is not null
                && lessonKinds.Contains(candidate.Kind)
                && candidate.AssistantEnabled
                && candidate.Access == CourseItemAccessReason.Enrolled;
        }

        if (candidate.Found && candidate.IsCourseItem)
        {
            return candidate.CanView;
        }

        return true;
    }
}
