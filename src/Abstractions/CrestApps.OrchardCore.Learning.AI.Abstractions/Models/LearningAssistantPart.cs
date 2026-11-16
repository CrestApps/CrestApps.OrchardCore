using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.AI.Models;

/// <summary>
/// The learning assistant switch of a course. It is off until an author turns it on; while it is off, the tutor
/// retrieves nothing from the course.
/// </summary>
public sealed class LearningAssistantPart : ContentPart
{
    /// <summary>
    /// Gets or sets whether the tutor may answer the course's learners from its lessons.
    /// </summary>
    public bool Enabled { get; set; }
}
