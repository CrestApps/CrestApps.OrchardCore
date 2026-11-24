namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// One thing a certification path asks for, such as a course completed or a quiz passed.
/// </summary>
public sealed class CertificationRequirement
{
    /// <summary>
    /// Gets or sets the id of the requirement within its path.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the kind, such as <c>CourseCompleted</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets what the requirement is about, such as the content item id of a course or a quiz.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the lowest grade or percent that counts; <see langword="null"/> takes any pass.
    /// </summary>
    public decimal? MinimumGrade { get; set; }
}
