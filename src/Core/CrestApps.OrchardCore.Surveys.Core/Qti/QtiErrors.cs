namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// Why a whole file could not be read as QTI.
/// </summary>
public static class QtiErrors
{
    /// <summary>The file is neither a zip archive nor an XML file.</summary>
    public const string NotQti = "NotQti";

    /// <summary>The archive was refused by the safe extraction rules; the detail names the rule and the entry.</summary>
    public const string UnsafeArchive = "UnsafeArchive";

    /// <summary>The archive has no <c>imsmanifest.xml</c> at its root.</summary>
    public const string NoManifest = "NoManifest";

    /// <summary>The manifest is not well-formed XML, or it has a DTD.</summary>
    public const string InvalidManifest = "InvalidManifest";

    /// <summary>The manifest lists no QTI 2.1 or QTI 3.0 assessment item.</summary>
    public const string NoItems = "NoItems";

    /// <summary>The package has more items than allowed.</summary>
    public const string TooManyItems = "TooManyItems";
}
