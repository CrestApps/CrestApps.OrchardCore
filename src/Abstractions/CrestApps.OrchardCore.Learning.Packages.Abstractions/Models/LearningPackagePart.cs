using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// A learning package in a course outline: the imported package file in the protected store and what its manifest says
/// about the item that is launched. Every value is set by the import, never by the editor.
/// </summary>
public sealed class LearningPackagePart : ContentPart
{
    /// <summary>
    /// Gets or sets the id of the package file in the protected store, or <see langword="null"/> before a package is
    /// imported.
    /// </summary>
    public string PackageFileId { get; set; }

    /// <summary>
    /// Gets or sets the name of the file the author uploaded.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the standard of the package, detected from its manifest: <see cref="PackagesConstants.Standards.Scorm12"/>,
    /// <see cref="PackagesConstants.Standards.Scorm2004ThirdEdition"/> or <see cref="PackagesConstants.Standards.Scorm2004FourthEdition"/>.
    /// </summary>
    public string Standard { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the manifest.
    /// </summary>
    public string ManifestIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the title of the package, from its default organization.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the manifest item that is launched.
    /// </summary>
    public string LaunchItemIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the title of the manifest item that is launched.
    /// </summary>
    public string LaunchItemTitle { get; set; }

    /// <summary>
    /// Gets or sets the path of the launched file inside the package, with <c>xml:base</c> applied.
    /// </summary>
    public string LaunchPath { get; set; }

    /// <summary>
    /// Gets or sets the query string the launched file is opened with (the resource's own and the item's parameters),
    /// without the leading <c>?</c>.
    /// </summary>
    public string LaunchQuery { get; set; }

    /// <summary>
    /// Gets or sets the SCORM 1.2 mastery score of the launched item (<c>adlcp:masteryscore</c>), from 0 to 100.
    /// </summary>
    public decimal? MasteryScore { get; set; }

    /// <summary>
    /// Gets or sets the time the learner is allowed: <c>adlcp:maxtimeallowed</c> as a CMITimespan (SCORM 1.2), or the
    /// <c>attemptAbsoluteDurationLimit</c> of the item's limit conditions as an ISO 8601 duration (SCORM 2004).
    /// </summary>
    public string MaxTimeAllowed { get; set; }

    /// <summary>
    /// Gets or sets what the content does when the time is up (<c>adlcp:timelimitaction</c>, or <c>adlcp:timeLimitAction</c> in SCORM 2004).
    /// </summary>
    public string TimeLimitAction { get; set; }

    /// <summary>
    /// Gets or sets the launch data of the item (<c>adlcp:datafromlms</c>, or <c>adlcp:dataFromLMS</c> in SCORM 2004).
    /// </summary>
    public string LaunchData { get; set; }

    /// <summary>
    /// Gets or sets the SCORM 2004 completion threshold of the launched item (<c>adlcp:completionThreshold</c>), from 0 to
    /// 1; <c>cmi.completion_threshold</c>.
    /// </summary>
    public decimal? CompletionThreshold { get; set; }

    /// <summary>
    /// Gets or sets the SCORM 2004 scaled passing score of the launched item (the <c>imsss:minNormalizedMeasure</c> of its
    /// primary objective when it is satisfied by measure), from -1 to 1; <c>cmi.scaled_passing_score</c>.
    /// </summary>
    public decimal? ScaledPassingScore { get; set; }

    /// <summary>
    /// Gets or sets the ids of the SCORM 2004 objectives of the launched item, which a new attempt starts with in
    /// <c>cmi.objectives</c>.
    /// </summary>
    public IList<string> ObjectiveIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the SCORM 2004 manifest has sequencing information. Sequencing and Navigation are not
    /// implemented, so it is not applied.
    /// </summary>
    public bool HasSequencing { get; set; }

    /// <summary>
    /// Gets or sets how many SCOs the default organization has. Only the first one is launched.
    /// </summary>
    public int ScoCount { get; set; }

    /// <summary>
    /// Gets or sets how many files the package has.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Gets or sets the size of the files once extracted, in bytes.
    /// </summary>
    public long ExpandedSize { get; set; }

    /// <summary>
    /// Gets or sets when the package was imported, in UTC.
    /// </summary>
    public DateTime? ImportedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person who imported it.
    /// </summary>
    public string ImportedBy { get; set; }
}
