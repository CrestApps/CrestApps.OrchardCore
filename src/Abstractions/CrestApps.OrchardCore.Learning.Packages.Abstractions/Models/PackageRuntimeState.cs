using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// A learner's runtime state on a learning package under one enrollment: the SCORM 1.2 or SCORM 2004 data the content
/// keeps, the sessions and attempts, and the launch that may write to it now. One per enrollment and package item. A
/// state written before SCORM 2004 was supported has no <see cref="Standard"/> and is SCORM 1.2.
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

    /// <summary>
    /// Gets or sets the standard of the package at the last launch, one of <see cref="PackagesConstants.Standards"/>;
    /// <see langword="null"/> for a state written before SCORM 2004 was supported, which is SCORM 1.2.
    /// </summary>
    public string Standard { get; set; }

    /// <summary>Gets or sets the SCORM 1.2 data. A SCORM 2004 state stores none and never reads it (it reads back as new
    /// SCORM 1.2 data); the <see cref="Standard"/> says which data a state uses.</summary>
    public Scorm12Cmi Cmi { get; set; } = new();

    /// <summary>Gets or sets the SCORM 2004 data; <see langword="null"/> for a SCORM 1.2 state.</summary>
    public Scorm2004Cmi Cmi2004 { get; set; }

    /// <summary>Gets or sets the cmi5 state; <see langword="null"/> for a SCORM state.</summary>
    public Cmi5State Cmi5 { get; set; }

    /// <summary>Gets or sets how many times the learner launched the package.</summary>
    public int Sessions { get; set; }

    /// <summary>
    /// Gets or sets how many attempts a SCORM 2004 learner started: the first launch and every launch after an exit
    /// other than <c>suspend</c> start one. SCORM 1.2 counts sessions only.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets the launch that may write now; an older launch, such as another tab, is refused.</summary>
    public string CurrentLaunchId { get; set; }

    /// <summary>
    /// Gets or sets the number of the last runtime call applied in the current launch; a new launch starts again at 0.
    /// </summary>
    public long LastSequence { get; set; }

    /// <summary>Gets or sets when the package was first launched, in UTC.</summary>
    public DateTime? FirstLaunchedUtc { get; set; }

    /// <summary>Gets or sets when the package was last launched, in UTC.</summary>
    public DateTime? LastLaunchedUtc { get; set; }

    /// <summary>Gets or sets when the content last committed its data, in UTC.</summary>
    public DateTime? LastCommitUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the result of the last commit or finish did not reach the learner's progress because the
    /// enrollment stayed busy. The next launch records it before it starts a session, which for SCORM 2004 can start a new
    /// attempt and reset the data the result comes from.
    /// </summary>
    public bool ResultNotRecorded { get; set; }

    /// <summary>Gets or sets when a session last finished, in UTC.</summary>
    public DateTime? LastFinishedUtc { get; set; }

    /// <summary>Gets or sets the result of each attempt, for a package with attempt settings, oldest first.</summary>
    public IList<PackageAttemptScore> AttemptScores { get; set; } = [];
}

/// <summary>
/// The result of one attempt on a learning package.
/// </summary>
public sealed class PackageAttemptScore
{
    /// <summary>Gets or sets the number of the attempt, from 1.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the score, as a percentage, or <see langword="null"/> without one.</summary>
    public decimal? Score { get; set; }

    /// <summary>Gets or sets whether the attempt passed, failed, or neither.</summary>
    public bool? Passed { get; set; }

    /// <summary>Gets or sets whether the package reported the attempt complete.</summary>
    public bool Completed { get; set; }
}
