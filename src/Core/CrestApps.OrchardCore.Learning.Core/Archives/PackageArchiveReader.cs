using System.IO.Compression;

namespace CrestApps.OrchardCore.Learning.Core.Archives;

/// <summary>
/// Reads the files of an uploaded package without extracting it, under the same rules as
/// <see cref="PackageArchiveExtractor"/>: every entry is checked when the archive is opened (no absolute path, no
/// <c>..</c>, no symbolic link, no duplicate, no more entries than allowed), and the bytes read are counted against
/// <see cref="PackageArchiveLimits.MaxExpandedBytes"/> as they are read.
/// </summary>
public sealed class PackageArchiveReader : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly PackageArchiveLimits _limits;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;

    private PackageArchiveReader(ZipArchive zip, PackageArchiveLimits limits, List<(ZipArchiveEntry Entry, string Path)> entries)
    {
        _zip = zip;
        _limits = limits;
        Entries = entries;
        Files = entries.Select(item => item.Path).ToArray();
        _byPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entry, path) in entries)
        {
            _byPath[path] = entry;
        }
    }

    /// <summary>
    /// Gets the paths of the files, normalized with <see cref="PackagePath"/>.
    /// </summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>
    /// Gets the bytes read so far.
    /// </summary>
    public long ExpandedSize { get; private set; }

    internal IReadOnlyList<(ZipArchiveEntry Entry, string Path)> Entries { get; }

    /// <summary>
    /// Opens an archive, checking every entry first.
    /// </summary>
    /// <param name="archive">The archive, readable and seekable; it stays open.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="refusal">Why the archive was refused, or <see langword="null"/>.</param>
    /// <returns>The reader, or <see langword="null"/> when the archive was refused.</returns>
    public static PackageArchiveReader Open(Stream archive, PackageArchiveLimits limits, out PackageArchiveResult refusal)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(limits);

        ZipArchive zip;

        try
        {
            zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            refusal = PackageArchiveResult.Fail(PackageArchiveErrors.InvalidArchive);

            return null;
        }

        refusal = Check(zip, limits, out var entries);

        if (refusal is not null)
        {
            zip.Dispose();

            return null;
        }

        return new PackageArchiveReader(zip, limits, entries);
    }

    /// <summary>
    /// Gets whether the archive has a file at a path, ignoring case.
    /// </summary>
    /// <param name="path">The path, normalized or not.</param>
    public bool Contains(string path)
        => PackagePath.Normalize(path) is { } normalized && _byPath.ContainsKey(normalized);

    /// <summary>
    /// Reads a file.
    /// </summary>
    /// <param name="path">The path, normalized or not; case is ignored.</param>
    /// <param name="content">The bytes of the file.</param>
    /// <param name="error">Why it was not read: <see cref="PackageArchiveErrors.TooLarge"/> once the bytes read pass the
    /// limit, <see cref="PackageArchiveErrors.InvalidArchive"/> for a damaged entry, or <see langword="null"/> when the
    /// archive has no such file.</param>
    /// <returns>Whether the file was read.</returns>
    public bool TryRead(string path, out byte[] content, out string error)
    {
        content = null;
        error = null;

        if (PackagePath.Normalize(path) is not { } normalized || !_byPath.TryGetValue(normalized, out var entry))
        {
            return false;
        }

        try
        {
            using var source = entry.Open();
            using var target = new MemoryStream();
            var buffer = new byte[81920];
            int read;

            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                ExpandedSize += read;

                // Counted as read: the size an entry claims is never trusted.
                if (ExpandedSize > _limits.MaxExpandedBytes)
                {
                    error = PackageArchiveErrors.TooLarge;

                    return false;
                }

                target.Write(buffer, 0, read);
            }

            content = target.ToArray();

            return true;
        }
        catch (InvalidDataException)
        {
            error = PackageArchiveErrors.InvalidArchive;

            return false;
        }
    }

    /// <summary>
    /// Checks every entry of an archive before anything is read or written.
    /// </summary>
    internal static PackageArchiveResult Check(ZipArchive zip, PackageArchiveLimits limits, out List<(ZipArchiveEntry Entry, string Path)> plan)
    {
        plan = [];

        var entries = zip.Entries;

        if (entries.Count > limits.MaxEntries)
        {
            return PackageArchiveResult.Fail(PackageArchiveErrors.TooManyEntries);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (IsSymbolicLink(entry))
            {
                return PackageArchiveResult.Fail(PackageArchiveErrors.SymbolicLink, entry.FullName);
            }

            // A folder entry holds nothing; its files carry their own paths.
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

            if (!seen.Add(path))
            {
                return PackageArchiveResult.Fail(PackageArchiveErrors.DuplicateEntry, entry.FullName);
            }

            plan.Add((entry, path));
        }

        return plan.Count == 0
            ? PackageArchiveResult.Fail(PackageArchiveErrors.Empty)
            : null;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        // An archive made on Unix keeps the file mode in the high word of the external attributes.
        const int fileTypeMask = 0xF000;
        const int symbolicLink = 0xA000;

        return ((entry.ExternalAttributes >> 16) & fileTypeMask) == symbolicLink;
    }

    /// <inheritdoc />
    public void Dispose()
        => _zip.Dispose();
}
