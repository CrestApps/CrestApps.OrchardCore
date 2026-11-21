using System.Formats.Tar;
using System.IO.Compression;

namespace CrestApps.OrchardCore.Archives;

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
    public static Task<PackageArchiveResult> ExtractAsync(Stream archive, string targetDirectory, PackageArchiveLimits limits, CancellationToken cancellationToken = default)
        => Task.FromResult(Extract(archive, targetDirectory, limits, cancellationToken));

    /// <summary>
    /// Extracts a zip archive, as <see cref="ExtractAsync"/> does, for a caller that reads synchronously (a
    /// ContentTransfer file reader).
    /// </summary>
    /// <param name="archive">The archive, readable and seekable.</param>
    /// <param name="targetDirectory">The empty folder to extract into.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was extracted, or why the archive was refused.</returns>
    public static PackageArchiveResult Extract(Stream archive, string targetDirectory, PackageArchiveLimits limits, CancellationToken cancellationToken = default)
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
            // Every entry is checked before the first byte is written, by the rules the archive reader also applies.
            var refusal = PackageArchiveReader.Check(zip, limits, out var plan);

            if (refusal is not null)
            {
                return refusal;
            }

            var root = Path.GetFullPath(targetDirectory);
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

            foreach (var (entry, path) in plan)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));

                // A second guard that does not depend on the path rule: the file must land inside the folder.
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.FullName);
                }
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

                    using var source = entry.Open();
                    using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    int read;

                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        written += read;

                        // Counted as written: the size an entry claims is never trusted.
                        if (written > limits.MaxExpandedBytes)
                        {
                            return PackageArchiveResult.Fail(PackageArchiveErrors.TooLarge, entry.FullName);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        target.Write(buffer, 0, read);
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

    /// <summary>
    /// Extracts a gzip-compressed tar archive (such as a Moodle course backup of Moodle 2.6 and later) by the same rules
    /// as a zip archive: every entry's path is checked with <see cref="PackagePath"/> before it is written, links,
    /// devices and other special entries are refused, as are duplicates and more entries or bytes than allowed. A tar
    /// archive is read in one pass, so a refusal can come after some files were written; the caller deletes the folder,
    /// and nothing outside it was written.
    /// </summary>
    /// <param name="archive">The archive, readable.</param>
    /// <param name="targetDirectory">The empty folder to extract into.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was extracted, or why the archive was refused.</returns>
    public static PackageArchiveResult ExtractTarGz(Stream archive, string targetDirectory, PackageArchiveLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrEmpty(targetDirectory);
        ArgumentNullException.ThrowIfNull(limits);

        var root = Path.GetFullPath(targetDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        var buffer = new byte[81920];
        long written = 0;
        var count = 0;
        string current = null;

        try
        {
            using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
            using var tar = new TarReader(gzip, leaveOpen: true);

            while (tar.GetNextEntry(copyData: false) is { } entry)
            {
                current = entry.Name;

                // A global header of pax attributes describes the archive, not a file.
                if (entry.EntryType == TarEntryType.GlobalExtendedAttributes)
                {
                    continue;
                }

                if (++count > limits.MaxEntries)
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.TooManyEntries);
                }

                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        continue;

                    case TarEntryType.SymbolicLink or TarEntryType.HardLink:
                        return PackageArchiveResult.Fail(PackageArchiveErrors.SymbolicLink, entry.Name);

                    case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                        break;

                    default:
                        return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.Name);
                }

                var path = PackagePath.Normalize(entry.Name);

                if (path is null)
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.Name);
                }

                if (!seen.Add(path))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.DuplicateEntry, entry.Name);
                }

                var fullPath = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));

                // A second guard that does not depend on the path rule: the file must land inside the folder.
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return PackageArchiveResult.Fail(PackageArchiveErrors.UnsafePath, entry.Name);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                using (var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (entry.DataStream is { } source)
                    {
                        int read;

                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            written += read;

                            // Counted as written: the size an entry claims is never trusted.
                            if (written > limits.MaxExpandedBytes)
                            {
                                return PackageArchiveResult.Fail(PackageArchiveErrors.TooLarge, entry.Name);
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            target.Write(buffer, 0, read);
                        }
                    }
                }

                files.Add(path);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or EndOfStreamException)
        {
            return PackageArchiveResult.Fail(PackageArchiveErrors.InvalidArchive, current);
        }
        catch (IOException)
        {
            // A file path that is also a folder of another entry (a/b and a/b/c).
            return PackageArchiveResult.Fail(PackageArchiveErrors.DuplicateEntry, current);
        }

        return files.Count == 0
            ? PackageArchiveResult.Fail(PackageArchiveErrors.Empty)
            : new PackageArchiveResult { Files = files, ExpandedSize = written };
    }
}
