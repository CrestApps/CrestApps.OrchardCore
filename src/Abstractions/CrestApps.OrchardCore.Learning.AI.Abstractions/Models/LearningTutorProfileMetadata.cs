namespace CrestApps.OrchardCore.Learning.AI.Models;

/// <summary>
/// Marks an AI profile as a learning tutor, stored in the profile's properties. A tutor retrieves only lessons of the
/// courses the learner is actively enrolled in whose learning assistant is on, and never exam content.
/// </summary>
public sealed class LearningTutorProfileMetadata
{
    /// <summary>
    /// Gets or sets whether the profile is a learning tutor.
    /// </summary>
    public bool IsTutor { get; set; }
}
