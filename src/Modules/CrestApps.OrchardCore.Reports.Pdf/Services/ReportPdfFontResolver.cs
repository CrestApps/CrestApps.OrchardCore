namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Finds the installed TrueType font a report PDF is written in: the family a document asks for when it is installed,
/// otherwise the installed font that stands in for it — Liberation Sans, Serif and Mono first, because they have the
/// widths of Arial, Times New Roman and Courier New, so a document lays out as it does on Windows — and the bold face
/// of that font. It reads the font folders of Windows, Linux and macOS.
/// </summary>
/// <remarks>
/// Faces are told apart by their file names: <c>LiberationSans-Bold.ttf</c> on Linux and macOS, and
/// <c>arialbd.ttf</c> beside <c>arial.ttf</c> on Windows. When no font of a kind is installed, any installed font is
/// used rather than failing the document.
/// </remarks>
internal sealed class ReportPdfFontResolver
{
    private const int MaxFontFiles = 5000;

    private static readonly string[] _fontDirectories =
    [
        "/usr/share/fonts",
        "/usr/local/share/fonts",
        "/Library/Fonts",
        "/System/Library/Fonts",
    ];

    // Families are compared by their letters and digits only, lowercased: "Liberation Sans" and
    // "LiberationSans-Bold.ttf" both read "liberationsans".
    private static readonly string[] _sansFamilies = ["liberationsans", "arimo", "arial", "helvetica", "dejavusans", "freesans", "notosans", "opensans", "roboto", "ubuntu"];
    private static readonly string[] _serifFamilies = ["liberationserif", "tinos", "timesnewroman", "times", "dejavuserif", "freeserif", "notoserif"];
    private static readonly string[] _monoFamilies = ["liberationmono", "cousine", "couriernew", "courier", "dejavusansmono", "freemono", "notosansmono", "ubuntumono"];

    // The one-letter suffixes are the ones of the Windows fonts, such as "consolab" and "consolaz".
    private static readonly (string Suffix, FontStyle Style)[] _styleSuffixes =
    [
        ("bolditalic", FontStyle.BoldItalic),
        ("boldoblique", FontStyle.BoldItalic),
        ("bi", FontStyle.BoldItalic),
        ("bold", FontStyle.Bold),
        ("bd", FontStyle.Bold),
        ("italic", FontStyle.Italic),
        ("oblique", FontStyle.Italic),
        ("it", FontStyle.Italic),
        ("regular", FontStyle.Regular),
        ("book", FontStyle.Regular),
        ("roman", FontStyle.Regular),
        ("b", FontStyle.Bold),
        ("i", FontStyle.Italic),
        ("z", FontStyle.BoldItalic),
    ];

    private readonly Dictionary<string, Dictionary<FontStyle, string>> _families;
    private readonly Dictionary<string, string> _files;
    private readonly string _fallbackFamily;

    private ReportPdfFontResolver(Dictionary<string, Dictionary<FontStyle, string>> families, Dictionary<string, string> files, string fallbackFamily)
    {
        _families = families;
        _files = files;
        _fallbackFamily = fallbackFamily;
    }

    /// <summary>
    /// The styles a font file can hold.
    /// </summary>
    internal enum FontStyle
    {
        /// <summary>
        /// Upright, normal weight.
        /// </summary>
        Regular,

        /// <summary>
        /// Bold.
        /// </summary>
        Bold,

        /// <summary>
        /// Italic or oblique.
        /// </summary>
        Italic,

        /// <summary>
        /// Bold and italic.
        /// </summary>
        BoldItalic,
    }

    /// <summary>
    /// Creates a resolver over the fonts installed on this machine.
    /// </summary>
    /// <param name="resolver">The resolver, when any font is installed.</param>
    /// <returns><see langword="true"/> when a font was found.</returns>
    public static bool TryCreate(out ReportPdfFontResolver resolver)
    {
        resolver = FromFiles(FindFontFiles());

        return resolver is not null;
    }

    /// <summary>
    /// Creates a resolver over the given font files.
    /// </summary>
    /// <param name="paths">The TrueType files.</param>
    /// <returns>The resolver, or <see langword="null"/> when there is no file.</returns>
    internal static ReportPdfFontResolver FromFiles(IEnumerable<string> paths)
    {
        var faces = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in paths ?? [])
        {
            var faceName = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(faceName))
            {
                faces.TryAdd(faceName, path);
            }
        }

        // A Windows face, such as "arialbd", is read as a style of the family whose regular file is installed.
        var names = faces.Keys.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var families = new Dictionary<string, Dictionary<FontStyle, string>>(StringComparer.Ordinal);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (faceName, path) in faces)
        {
            var (family, style) = Parse(faceName, names);

            if (family.Length == 0)
            {
                continue;
            }

            files[faceName] = path;

            if (!families.TryGetValue(family, out var styles))
            {
                styles = [];
                families[family] = styles;
            }

            styles.TryAdd(style, faceName);
        }

        if (files.Count == 0)
        {
            return null;
        }

        var fallback = _sansFamilies.FirstOrDefault(families.ContainsKey)
            ?? families.Keys.Where(key => key.Contains("sans", StringComparison.Ordinal) && !key.Contains("mono", StringComparison.Ordinal)).Order(StringComparer.Ordinal).FirstOrDefault()
            ?? families.Keys.Order(StringComparer.Ordinal).First();

        return new ReportPdfFontResolver(families, files, fallback);
    }

    /// <summary>
    /// Chooses the installed file for a family and weight.
    /// </summary>
    /// <param name="familyName">The family the document asks for.</param>
    /// <param name="isBold">Whether it asks for bold.</param>
    /// <returns>The path of the file, and whether the font has no bold face, so bold has to be simulated.</returns>
    public (string Path, bool SimulateBold) Resolve(string familyName, bool isBold)
    {
        var family = Choose(Normalize(familyName));
        var (faceName, style) = Face(family, isBold ? FontStyle.Bold : FontStyle.Regular);

        return (_files[faceName], isBold && style is not (FontStyle.Bold or FontStyle.BoldItalic));
    }

    private string Choose(string requested)
    {
        if (requested.Length > 0 && _families.ContainsKey(requested))
        {
            return requested;
        }

        var candidates = IsMono(requested)
            ? _monoFamilies
            : IsSerif(requested)
                ? _serifFamilies
                : _sansFamilies;

        return candidates.FirstOrDefault(_families.ContainsKey) ?? _fallbackFamily;
    }

    private (string FaceName, FontStyle Style) Face(string family, FontStyle wanted)
    {
        var styles = _families[family];

        if (styles.TryGetValue(wanted, out var exact))
        {
            return (exact, wanted);
        }

        // Without a bold italic face, the bold one is closer than the italic one: weight shows more.
        var nearest = wanted switch
        {
            FontStyle.BoldItalic => new[] { FontStyle.Bold, FontStyle.Italic, FontStyle.Regular },
            FontStyle.Bold or FontStyle.Italic => [FontStyle.Regular],
            _ => [FontStyle.Bold, FontStyle.Italic, FontStyle.BoldItalic],
        };

        foreach (var style in nearest)
        {
            if (styles.TryGetValue(style, out var face))
            {
                return (face, style);
            }
        }

        var any = styles.First();

        return (any.Value, any.Key);
    }

    private static (string Family, FontStyle Style) Parse(string faceName, HashSet<string> names)
    {
        var normalized = Normalize(faceName);

        foreach (var (suffix, style) in _styleSuffixes)
        {
            // A suffix only counts as a style when something is left for the family: "Bold.ttf" is not a family.
            if (normalized.Length > suffix.Length + 2 && normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                var family = normalized[..^suffix.Length];

                // "Italic" inside "BoldItalic" was matched first. A short suffix needs the separator the file name
                // wrote, or the regular file of its family beside it: "Credit.ttf" is not an italic "Cred", but
                // "arialbd.ttf" is the bold "arial".
                if (suffix.Length <= 2 && !HasSeparatorBefore(faceName, suffix.Length) && !names.Contains(family))
                {
                    continue;
                }

                return (family, style);
            }
        }

        return (normalized, FontStyle.Regular);
    }

    private static bool HasSeparatorBefore(string faceName, int suffixLength)
    {
        var index = faceName.Length - suffixLength - 1;

        return index >= 0 && faceName[index] is '-' or '_' or ' ';
    }

    private static string Normalize(string name)
    {
        return new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static bool IsMono(string family)
    {
        return family.Contains("mono", StringComparison.Ordinal) ||
            family.Contains("courier", StringComparison.Ordinal) ||
            family.Contains("consol", StringComparison.Ordinal) ||
            family.Contains("menlo", StringComparison.Ordinal) ||
            family.Contains("code", StringComparison.Ordinal);
    }

    private static bool IsSerif(string family)
    {
        return (family.Contains("serif", StringComparison.Ordinal) && !family.Contains("sansserif", StringComparison.Ordinal)) ||
            family.Contains("times", StringComparison.Ordinal) ||
            family.Contains("georgia", StringComparison.Ordinal) ||
            family.Contains("garamond", StringComparison.Ordinal) ||
            family.Contains("cambria", StringComparison.Ordinal) ||
            family.Contains("palatino", StringComparison.Ordinal) ||
            family.Contains("bookman", StringComparison.Ordinal);
    }

    private static List<string> FindFontFiles()
    {
        var files = new List<string>();
        var directories = new List<string>();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrEmpty(windows))
        {
            directories.Add(windows);
        }

        if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(local))
        {
            directories.Add(Path.Combine(local, "Microsoft", "Windows", "Fonts"));
        }

        directories.AddRange(_fontDirectories);

        if (!string.IsNullOrEmpty(home))
        {
            directories.Add(Path.Combine(home, ".fonts"));
            directories.Add(Path.Combine(home, ".local", "share", "fonts"));
            directories.Add(Path.Combine(home, "Library", "Fonts"));
        }

        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                files.AddRange(Directory.EnumerateFiles(directory, "*.ttf", SearchOption.AllDirectories).Take(MaxFontFiles - files.Count));
            }
            catch (IOException)
            {
                // A directory that cannot be read is skipped; the others may still hold fonts.
            }
            catch (UnauthorizedAccessException)
            {
                // Likewise.
            }

            if (files.Count >= MaxFontFiles)
            {
                break;
            }
        }

        return files;
    }
}
