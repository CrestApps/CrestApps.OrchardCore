namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// The options of the learning assistant.
/// </summary>
public sealed class LearningAIOptions
{
    /// <summary>
    /// Gets the course item kinds a tutor may answer from. The default is the page, video and file lessons; quizzes,
    /// coursework, packages, external tools and classes are never lessons for the tutor.
    /// </summary>
    public HashSet<string> TutorLessonKinds { get; } = new(StringComparer.Ordinal)
    {
        LearningConstants.CourseItemKinds.Page,
        LearningConstants.CourseItemKinds.Video,
        LearningConstants.CourseItemKinds.File,
    };
}
