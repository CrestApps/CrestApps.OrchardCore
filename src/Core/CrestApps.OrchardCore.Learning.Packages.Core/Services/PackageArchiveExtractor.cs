using System.IO.Compression;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// The limits an uploaded package must stay within.
/// </summary>
public sealed class PackageArchiveLimits
{
    /// <summary>
    /// Gets or sets the most entries the archive may have, folders included.
    /// </summary>
    public int MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the most bytes the files may take once extracted. The bytes are counted as they are written, so a
    /// size the archive claims for an entry is never trusted.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = 1024L * 1024 * 1024;
}

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

/// <summary>
/// Extracts an uploaded package into a folder, refusing what could write outside it or exhaust the disk: an absolute
/// path or one with <c>..</c>, a symbolic link, too many entries, or more bytes than allowed.
/// </summary>
public static class PackageArchiveExtractor
{
    /// <summary>
    /// Extracts an archive. On a refusal the caller deletes the folder; nothing outside it was written.
    /// </summary>
    /// <param name="archive">The archive, readable and seekable.</param>
    /// <param name="targetDirectory">The empty folder to extract into.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was extracted, or why the archive was refused.</returns>
    public static async Task<PackageArchiveResult> ExtractAsync(Stream archive, string targetDirectory, PackageArchiveLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        ArgumentNullException.ThrowIfNull(limits);

        ZipArchive zip;

        try
        {
            zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return PackageArchiveResult.Fail(PackageArchiveErrors.InvalidArchive);
        }

        using (zip)
        {
            var entries = zip.Entries;

            if (entries.Count > limits.MaxEntries)
            {
                return PackageArchiveResult.Fail(PackageArchiveErrors.TooManyEntries);
            }

            var root = Path.GetFullPath(targetDirectory);
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

            // Every entry is checked before the first byte is written.
            var plan = new List<(ZipArchiveEntry Entry, string Path)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (IsSymbolicLink(entry))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.SymbolicLink, entry.FullName);
                }

                // A folder entry writes nothing; its files carry their own paths.
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    if (entry.Length > 0)
                    {
                        return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.FullName);
                    }

                    continue;
                }

                var path = PackagePath.Normalize(entry.FullName);

                if (path is null)
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.FullName);
                }

                var fullPath = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));

                // A second guard that does not depend on the path rule: the file must land inside the folder.
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.FullName);
                }

                if (!seen.Add(path))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.DuplicateEntry, entry.FullName);
                }

                plan.Add((entry, path));
            }

            if (plan.Count == 0)
            {
                return PackageArchiveResult.Fail(PackageArchiveErrors.Empty);
            }

            long written = 0;
            var buffer = new byte[81920];

            foreach (var (entry, path) in plan)
            {
                // A file path that is also a folder of another entry (a/b and a/b/c) is caught by the file system.
                var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                    await using var source = entry.Open();
                    await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    int read;

                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        written += read;

                        // Counted as written: the size an entry claims is never trusted.
                        if (written > limits.MaxExpandedBytes)
                        {
                            return PackageArchiveResult.Fail(PackageArchiveErrors.TooLarge, entry.FullName);
                        }

                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }
                catch (InvalidDataException)
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.InvalidArchive, entry.FullName);
                }
                catch (IOException)
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.DuplicateEntry, entry.FullName);
                }
            }

            return new PackageArchiveResult
            {
                Files = plan.Select(item => item.Path).ToArray(),
                ExpandedSize = written,
            };
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        // An archive made on Unix keeps the file mode in the high word of the external attributes.
        const int fileTypeMask = 0xF000;
        const int symbolicLink = 0xA000;

        return ((entry.ExternalAttributes >> 16) & fileTypeMask) == symbolicLink;
    }
}
