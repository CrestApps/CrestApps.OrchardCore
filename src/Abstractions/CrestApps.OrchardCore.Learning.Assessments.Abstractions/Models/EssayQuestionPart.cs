using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// An essay question, which a grader marks.
/// </summary>
public sealed class EssayQuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the fewest words an answer needs, or zero.
    /// </summary>
    public int MinWords { get; set; }

    /// <summary>
    /// Gets or sets the most words an answer may have, or zero for no limit.
    /// </summary>
    public int MaxWords { get; set; }

    /// <summary>
    /// Gets or sets the guidance graders see. It is never shown to the learner.
    /// </summary>
    public string GraderGuidance { get; set; }
}
