using System.Formats.Tar;
using System.IO.Compression;

namespace CrestApps.OrchardCore.Archives;

/// <summary>
/// Why an archive was refused.
/// </summary>
public static class PackageArchiveErrors
{
    /// <summary>The file is not a zip archive.</summary>
    public const string InvalidArchive = "InvalidArchive";

    /// <summary>An entry has a path that is absolute, climbs out of the package or is not a safe file name.</summary>
    public const string UnsafePath = "UnsafePath";

    /// <summary>An entry is a symbolic link.</summary>
    public const string SymbolicLink = "SymbolicLink";

    /// <summary>The archive has more entries than allowed.</summary>
    public const string TooManyEntries = "TooManyEntries";

    /// <summary>The files are larger than allowed once extracted.</summary>
    public const string TooLarge = "TooLarge";

    /// <summary>Two entries have the same path, ignoring case.</summary>
    public const string DuplicateEntry = "DuplicateEntry";

    /// <summary>The archive has no files.</summary>
    public const string Empty = "Empty";
}
