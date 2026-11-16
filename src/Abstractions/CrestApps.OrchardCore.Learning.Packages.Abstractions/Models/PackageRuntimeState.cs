using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// A learner's runtime state on a learning package under one enrollment: the SCORM 1.2 data the content keeps, the
/// sessions, and the launch that may write to it now. One per enrollment and package item.
/// </summary>
public sealed class PackageRuntimeState : CatalogItem
{
    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the learning package course item.</summary>
    public string PackageItemId { get; set; }

    /// <summary>Gets or sets the title of the package item at the last launch, for lists.</summary>
    public string PackageTitle { get; set; }

    /// <summary>Gets or sets the package file the last launch ran.</summary>
    public string PackageFileId { get; set; }

    /// <summary>Gets or sets the SCORM 1.2 data.</summary>
    public Scorm12Cmi Cmi { get; set; } = new();

    /// <summary>Gets or sets how many times the learner launched the package.</summary>
    public int Sessions { get; set; }

    /// <summary>Gets or sets the launch that may write now; an older launch, such as another tab, is refused.</summary>
    public string CurrentLaunchId { get; set; }

    /// <summary>Gets or sets when the package was first launched, in UTC.</summary>
    public DateTime? FirstLaunchedUtc { get; set; }

    /// <summary>Gets or sets when the package was last launched, in UTC.</summary>
    public DateTime? LastLaunchedUtc { get; set; }

    /// <summary>Gets or sets when the content last committed its data, in UTC.</summary>
    public DateTime? LastCommitUtc { get; set; }

    /// <summary>Gets or sets when a session last finished, in UTC.</summary>
    public DateTime? LastFinishedUtc { get; set; }
}
