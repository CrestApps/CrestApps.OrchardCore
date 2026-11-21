using System.Formats.Tar;
using System.IO.Compression;

namespace CrestApps.OrchardCore.Archives;

/// <summary>
/// What an extraction did.
/// </summary>
public sealed class PackageArchiveResult
{
    /// <summary>Gets or sets the error, one of <see cref="PackageArchiveErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the entry that was refused, for the message.</summary>
    public string Entry { get; set; }

    /// <summary>Gets or sets the paths of the extracted files, normalized with <see cref="PackagePath"/>.</summary>
    public IReadOnlyList<string> Files { get; set; } = [];

    /// <summary>Gets or sets the bytes written.</summary>
    public long ExpandedSize { get; set; }

    /// <summary>Gets whether the archive was extracted.</summary>
    public bool Succeeded => Error is null;

    internal static PackageArchiveResult Fail(string error, string entry = null)
        => new() { Error = error, Entry = entry };
}
