namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// What the manifest (<c>imsmanifest.xml</c>) of a SCORM 1.2 package says: its identifier and title, the default
/// organization, and the SCOs of that organization in the order a learner meets them.
/// </summary>
public sealed class Scorm12Manifest
{
    /// <summary>Gets or sets the identifier of the manifest.</summary>
    public string Identifier { get; set; }

    /// <summary>Gets or sets the title of the default organization.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the schema version from the metadata, such as <c>1.2</c>.</summary>
    public string SchemaVersion { get; set; }

    /// <summary>Gets or sets the identifier of the organization that was read.</summary>
    public string OrganizationIdentifier { get; set; }

    /// <summary>Gets or sets the SCOs of the organization, depth first in document order.</summary>
    public IReadOnlyList<Scorm12Sco> Scos { get; set; } = [];

    /// <summary>Gets or sets how many items of the organization launch an asset rather than a SCO.</summary>
    public int AssetCount { get; set; }

    /// <summary>Gets the SCO that is launched: the first one.</summary>
    public Scorm12Sco Launch => Scos.Count > 0 ? Scos[0] : null;
}

/// <summary>
/// An item of the organization whose resource is a SCO.
/// </summary>
public sealed class Scorm12Sco
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

    /// <summary>Gets or sets the mastery score (<c>adlcp:masteryscore</c>), from 0 to 100.</summary>
    public decimal? MasteryScore { get; set; }

    /// <summary>Gets or sets <c>adlcp:maxtimeallowed</c>.</summary>
    public string MaxTimeAllowed { get; set; }

    /// <summary>Gets or sets <c>adlcp:timelimitaction</c>.</summary>
    public string TimeLimitAction { get; set; }

    /// <summary>Gets or sets <c>adlcp:datafromlms</c>.</summary>
    public string DataFromLms { get; set; }
}

/// <summary>
/// Why a manifest was refused.
/// </summary>
public static class Scorm12ManifestErrors
{
    /// <summary>The package has no <c>imsmanifest.xml</c> at its root.</summary>
    public const string Missing = "ManifestMissing";

    /// <summary>The manifest is not well-formed XML, or uses a DTD.</summary>
    public const string InvalidXml = "ManifestInvalidXml";

    /// <summary>The package is SCORM 2004, which this version does not run.</summary>
    public const string Scorm2004 = "Scorm2004NotSupported";

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
}

/// <summary>
/// The manifest read, or why it was refused.
/// </summary>
public sealed class Scorm12ManifestResult
{
    /// <summary>Gets or sets the manifest.</summary>
    public Scorm12Manifest Manifest { get; set; }

    /// <summary>Gets or sets the error, one of <see cref="Scorm12ManifestErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets whether the manifest was read.</summary>
    public bool Succeeded => Error is null;

    internal static Scorm12ManifestResult Fail(string error)
        => new() { Error = error };
}
