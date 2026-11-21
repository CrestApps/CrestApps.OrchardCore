namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// What the manifest (<c>imsmanifest.xml</c>) of a SCORM 1.2 or SCORM 2004 package says: the standard and edition, its
/// identifier and title, the default organization, and the SCOs of that organization in the order a learner meets them.
/// </summary>
public sealed class ScormManifest
{
    /// <summary>
    /// Gets or sets the standard of the package, one of <see cref="PackagesConstants.Standards"/>: SCORM 1.2, SCORM 2004
    /// 3rd Edition or SCORM 2004 4th Edition.
    /// </summary>
    public string Standard { get; set; }

    /// <summary>Gets or sets the identifier of the manifest.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the title of the default organization.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the schema version from the metadata, such as <c>1.2</c> or <c>2004 4th Edition</c>.</summary>
    public string SchemaVersion { get; set; }

    /// <summary>Gets or sets the identifier of the organization that was read.</summary>
    public string OrganizationIdentifier { get; set; }

    /// <summary>Gets or sets the SCOs of the organization, depth first in document order.</summary>
    public IReadOnlyList<ScormSco> Scos { get; set; } = [];

    /// <summary>Gets or sets how many items of the organization launch an asset rather than a SCO.</summary>
    public int AssetCount { get; set; }

    /// <summary>
    /// Gets or sets whether a SCORM 2004 manifest has sequencing information (<c>imsss:sequencing</c> on the organization
    /// or an item, or an <c>imsss:sequencingCollection</c>). Sequencing and Navigation are not implemented, so it is
    /// recorded and not applied.
    /// </summary>
    public bool HasSequencing { get; set; }

    /// <summary>Gets the SCO that is launched: the first one.</summary>
    public ScormSco Launch => Scos.Count > 0 ? Scos[0] : null;

    /// <summary>Gets whether the package is SCORM 2004.</summary>
    public bool IsScorm2004 => PackagesConstants.Standards.IsScorm2004(Standard);
}

/// <summary>
/// An item of the organization whose resource is a SCO.
/// </summary>
public sealed class ScormSco
{
    /// <summary>Gets or sets the identifier of the item.</summary>
    public string ItemIdentifier { get; set; }

    /// <summary>Gets or sets the title of the item.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the identifier of the resource.</summary>
    public string ResourceIdentifier { get; set; }

    /// <summary>Gets or sets the path of the launched file in the package, with every <c>xml:base</c> applied.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the query the file is opened with, without the leading <c>?</c>, or <see langword="null"/>.</summary>
    public string Query { get; set; }

    /// <summary>Gets or sets the SCORM 1.2 mastery score (<c>adlcp:masteryscore</c>), from 0 to 100.</summary>
    public decimal? MasteryScore { get; set; }

    /// <summary>
    /// Gets or sets the time the learner is allowed: <c>adlcp:maxtimeallowed</c> (a CMITimespan) in SCORM 1.2, the
    /// <c>attemptAbsoluteDurationLimit</c> of the item's <c>imsss:limitConditions</c> (an ISO 8601 duration) in SCORM 2004.
    /// </summary>
    public string MaxTimeAllowed { get; set; }

    /// <summary>Gets or sets <c>adlcp:timelimitaction</c> (SCORM 1.2) or <c>adlcp:timeLimitAction</c> (SCORM 2004).</summary>
    public string TimeLimitAction { get; set; }

    /// <summary>Gets or sets <c>adlcp:datafromlms</c> (SCORM 1.2) or <c>adlcp:dataFromLMS</c> (SCORM 2004).</summary>
    public string DataFromLms { get; set; }

    /// <summary>
    /// Gets or sets the SCORM 2004 completion threshold, from 0 to 1: the text of <c>adlcp:completionThreshold</c> (3rd
    /// Edition), or its <c>minProgressMeasure</c> when <c>completedByMeasure</c> is true (4th Edition).
    /// </summary>
    public decimal? CompletionThreshold { get; set; }

    /// <summary>
    /// Gets or sets the SCORM 2004 scaled passing score, from -1 to 1: the <c>imsss:minNormalizedMeasure</c> of the
    /// item's primary objective when it is <c>satisfiedByMeasure</c>.
    /// </summary>
    public decimal? ScaledPassingScore { get; set; }

    /// <summary>
    /// Gets or sets the ids of the SCORM 2004 objectives of the item (<c>imsss:objectives</c>), the primary objective first
    /// when it has an id. They are the objectives a SCO finds in <c>cmi.objectives</c> when an attempt starts.
    /// </summary>
    public IReadOnlyList<string> ObjectiveIds { get; set; } = [];

    /// <summary>Gets or sets whether the item has its own <c>imsss:sequencing</c>.</summary>
    public bool HasSequencing { get; set; }
}

/// <summary>
/// Why a manifest was refused.
/// </summary>
public static class ScormManifestErrors
{
    /// <summary>The package has no <c>imsmanifest.xml</c> at its root.</summary>
    public const string Missing = "ManifestMissing";

    /// <summary>The manifest is not well-formed XML, or uses a DTD.</summary>
    public const string InvalidXml = "ManifestInvalidXml";

    /// <summary>The package is a cmi5 course, which this version does not run.</summary>
    public const string Cmi5 = "Cmi5NotSupported";

    /// <summary>The manifest has no organization with items.</summary>
    public const string NoOrganization = "NoOrganization";

    /// <summary>No item of the organization launches a SCO.</summary>
    public const string NoSco = "NoSco";

    /// <summary>The SCO launches a URL outside the package.</summary>
    public const string ExternalLaunch = "ExternalLaunch";

    /// <summary>The SCO's file path is not a safe path inside the package.</summary>
    public const string UnsafeHref = "UnsafeHref";

    /// <summary>The SCO's launch file is not in the package.</summary>
    public const string LaunchFileMissing = "LaunchFileMissing";

    /// <summary>The mastery score is not a number from 0 to 100.</summary>
    public const string InvalidMasteryScore = "InvalidMasteryScore";

    /// <summary>A SCORM 2004 completion threshold is not a number from 0 to 1.</summary>
    public const string InvalidCompletionThreshold = "InvalidCompletionThreshold";

    /// <summary>A SCORM 2004 minimum normalized measure is not a number from -1 to 1.</summary>
    public const string InvalidPassingScore = "InvalidPassingScore";
}

/// <summary>
/// The manifest read, or why it was refused.
/// </summary>
public sealed class ScormManifestResult
{
    /// <summary>Gets or sets the manifest.</summary>
    public ScormManifest Manifest { get; set; }

    /// <summary>Gets or sets the error, one of <see cref="ScormManifestErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets whether the manifest was read.</summary>
    public bool Succeeded => Error is null;

    internal static ScormManifestResult Fail(string error)
        => new() { Error = error };
}
