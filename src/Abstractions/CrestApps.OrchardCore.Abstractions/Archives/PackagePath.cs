namespace CrestApps.OrchardCore.Archives;

/// <summary>
/// Makes the path of a file inside a package safe: relative, with forward slashes, and unable to leave the package.
/// The same rule reads the entries of an uploaded archive and the paths a package frame asks for.
/// </summary>
public static class PackagePath
{
    /// <summary>
    /// The longest path kept.
    /// </summary>
    public const int MaxLength = 512;

    private static readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Normalizes a path inside a package.
    /// </summary>
    /// <param name="path">The path, with either separator.</param>
    /// <returns>The path with forward slashes, or <see langword="null"/> when it is empty, absolute, climbs out of the
    /// package (a <c>..</c> segment), names a drive or a stream (a colon), holds a control character, or uses a name
    /// Windows keeps for a device or trims (a trailing dot or space).</returns>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxLength)
        {
            return null;
        }

        var unified = path.Replace('\\', '/');

        // Absolute on any system: a leading slash, a drive letter or a UNC share.
        if (unified[0] == '/')
        {
            return null;
        }

        var segments = new List<string>();

        foreach (var segment in unified.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == ".." || !IsSafeSegment(segment))
            {
                return null;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    private static bool IsSafeSegment(string segment)
    {
        if (segment.EndsWith('.') || segment.EndsWith(' '))
        {
            return false;
        }

        foreach (var character in segment)
        {
            if (char.IsControl(character) || character is ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                return false;
            }
        }

        var name = segment.Split('.')[0];

        return !_reservedNames.Contains(name);
    }
}
