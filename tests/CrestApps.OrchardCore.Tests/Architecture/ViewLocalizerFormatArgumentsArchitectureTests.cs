using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// In a Razor view <c>T</c> is an <c>IViewLocalizer</c>, and <c>T["Discount ({0})", name]</c> returns a
/// <c>LocalizedHtmlString</c>. Its arguments are applied only when the string is written as HTML content; its
/// <c>.Value</c> is the unformatted translation, so <c>T["Discount ({0})", name].Value</c> shows the user a literal
/// "{0}". This guard fails when a view under <c>src/Modules</c> reads <c>.Value</c> from a localized string that has
/// format placeholders and arguments. Write the <c>LocalizedHtmlString</c> itself where it is content, or use
/// <c>T.GetString("text {0}", arg).Value</c> where a plain string is needed (an attribute or a C# variable).
/// The reverse fails too: <c>T["No match for {0}"]</c> written as HTML, with no arguments, throws a
/// <c>FormatException</c> while the page renders. A placeholder meant for a script, such as bootstrap-select's
/// <c>{0}</c>, is passed as its own argument (<c>T["No match for {0}", "{0}"]</c>) or read with <c>.Value</c>.
/// </summary>
public sealed class ViewLocalizerFormatArgumentsArchitectureTests
{
    private static readonly Regex _placeholderRegex = new(@"\{\d+[,:}]", RegexOptions.Compiled);

    // The type argument may itself be generic (IHtmlLocalizer<A<B>>): the angle brackets are matched as balanced pairs.
    private static readonly Regex _htmlLocalizerDeclarationRegex = new(@"\bIHtmlLocalizer(?:\s*<(?>[^<>]+|<(?<depth>)|>(?<-depth>))*(?(depth)(?!))>)?\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);

    [Fact]
    public void Views_DoNotReadTheUnformattedValueOfALocalizedStringWithArguments()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        var violations = new List<string>();
        var scannedViewCount = 0;

        // Act
        foreach (var fullPath in SourceTreeFiles.Enumerate(modulesRoot, "*.cshtml", SourceTreeFiles.IsBuildOutputDirectory))
        {
            if (IsBuildOutput(fullPath))
            {
                continue;
            }

            scannedViewCount++;

            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');

            foreach (var line in FindFormattedValueReads(File.ReadAllText(fullPath)))
            {
                violations.Add($"{relativePath}:{line}");
            }
        }

        // Assert
        Assert.True(
            scannedViewCount > 0,
            "No .cshtml views were found under src/Modules, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These views read .Value from T[\"...{0}...\", args], which is the unformatted string, so the user sees " +
            "a literal placeholder. Write @T[...] as content, or use T.GetString(\"...\", args).Value for a plain string: " +
            $"{string.Join(", ", violations)}.");
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.Surveys*")]
    [InlineData("CrestApps.OrchardCore.Learning*")]
    public void Code_DoesNotReadTheUnformattedValueOfAnHtmlLocalizedStringWithArguments(string projects)
    {
        // Arrange: an IHtmlLocalizer's L["...{0}...", args].Value is the unformatted string as well, whatever the field,
        // property or injected name of the localizer (H by convention). A name may be declared in another file than the
        // one that uses it (a base class), so every name declared anywhere in the projects is looked for in every file.
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        var sources = new List<(string Path, string Source)>();

        foreach (var root in new[] { Path.Combine(repositoryRoot, "src", "Modules"), Path.Combine(repositoryRoot, "src", "Core") })
        {
            foreach (var directory in Directory.EnumerateDirectories(root, projects))
            {
                foreach (var fullPath in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                {
                    if (IsBuildOutput(fullPath) || !(fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || fullPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    sources.Add((Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(fullPath)));
                }
            }
        }

        var localizers = HtmlLocalizerNames(sources.Select(file => file.Source));

        // Act
        foreach (var (relativePath, source) in sources)
        {
            foreach (var violation in FindHtmlValueReads(source, localizers))
            {
                violations.Add($"{relativePath}:{violation}");
            }
        }

        // Assert
        Assert.True(sources.Count > 0, $"No {projects} source files were found, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These files read .Value from H[\"...{0}...\", args] (an IHtmlLocalizer), which is the unformatted string, so " +
            "the user sees a literal placeholder. Use H.GetString(\"...\", args).Value for a plain string: " +
            $"{string.Join(", ", violations)}.");
    }

    [Theory]
    [InlineData("""protected readonly IHtmlLocalizer H;""", "H")]
    [InlineData("""private readonly IHtmlLocalizer<AdminController> _htmlLocalizer;""", "_htmlLocalizer")]
    [InlineData("""public IHtmlLocalizer Html { get; }""", "Html")]
    [InlineData("""@inject IHtmlLocalizer<Startup> Labels""", "Labels")]
    [InlineData("""private readonly IStringLocalizer S;""", "H")]
    [InlineData("""private readonly IHtmlLocalizer<SurveyPage<SurveyQuestion>> Labels;""", "Labels")]
    [InlineData("""IHtmlLocalizer<Dictionary<string, List<int>>> nested""", "nested")]
    public void HtmlLocalizerNames_FindsEveryNameAnHtmlLocalizerIsDeclaredUnder(string source, string expected)
    {
        // Act
        var names = HtmlLocalizerNames([source]);

        // Assert
        Assert.Contains(expected, names);
    }

    [Fact]
    public void FindHtmlValueReads_FindsANameDeclaredInABaseClassInAnotherFile()
    {
        // Arrange (R8-Mi6): the base class declares the localizer under a name other than H; the derived class, in its
        // own file, reads .Value from it.
        const string baseClass = """public abstract class SurveyControllerBase { protected IHtmlLocalizer<SurveyControllerBase> Html { get; } }""";
        const string derived = """public sealed class SurveyController : SurveyControllerBase { void M() { var text = Html["{0} people", count].Value; } }""";
        var localizers = HtmlLocalizerNames([baseClass, derived]);

        // Act
        var violations = FindHtmlValueReads(derived, localizers);

        // Assert
        Assert.Single(violations);
    }

    [Theory]
    [InlineData("""ModelState.AddModelError(key, H["{0} is not an e-mail address.", email].Value);""", 1)]
    [InlineData("""ModelState.AddModelError(key, H.GetString("{0} is not an e-mail address.", email).Value);""", 0)]
    [InlineData("""await _notifier.SuccessAsync(H["{0} people were invited.", count]);""", 0)]
    [InlineData("""var html = OH["{0} people", count].Value;""", 0)]
    [InlineData("""private readonly IHtmlLocalizer _h; void M() { var text = _h["{0} people", count].Value; }""", 1)]
    public void FindFormattedValueReads_OfTheHtmlLocalizer_FlagsOnlyValueReadsWithArguments(string source, int expectedCount)
    {
        // Act
        var lines = FindHtmlValueReads(source, HtmlLocalizerNames([source]));

        // Assert
        Assert.Equal(expectedCount, lines.Count);
    }

    [Fact]
    public void Views_DoNotWriteALocalizedStringWithPlaceholdersButNoArguments()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var fullPath in SourceTreeFiles.Enumerate(Path.Combine(repositoryRoot, "src", "Modules"), "*.cshtml", SourceTreeFiles.IsBuildOutputDirectory))
        {
            if (IsBuildOutput(fullPath))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');

            foreach (var line in FindPlaceholdersWithoutArguments(File.ReadAllText(fullPath)))
            {
                violations.Add($"{relativePath}:{line}");
            }
        }

        // Assert
        Assert.True(
            violations.Count == 0,
            "These views write T[\"...{0}...\"] with no arguments, which throws a FormatException while the page " +
            "renders. Pass the placeholder as an argument (T[\"...{0}...\", \"{0}\"]) or read .Value for a script: " +
            $"{string.Join(", ", violations)}.");
    }

    [Theory]
    [InlineData("""data-none-results-text="@T["No permission matches {0}"]" """, 1)]
    [InlineData("""@T["Stage {0}"]""", 1)]
    [InlineData("""data-none-results-text="@T["No permission matches {0}", "{0}"]" """, 0)]
    [InlineData("""@Json.Serialize(T["Stage {0}"].Value)""", 0)]
    [InlineData("""@T["No placeholder"]""", 0)]
    [InlineData("""@(T["Stage {0}"])""", 1)]
    [InlineData("""notes.Add(Format(T["after {0}"], row.DependsOn));""", 0)]
    public void FindPlaceholdersWithoutArguments_FlagsOnlyWrittenStringsWithoutArguments(string source, int expectedCount)
    {
        // Act
        var lines = FindPlaceholdersWithoutArguments(source);

        // Assert
        Assert.Equal(expectedCount, lines.Count);
    }

    [Theory]
    [InlineData("""@(T["Discount ({0})", name].Value)""", 1)]
    [InlineData("""var x = T["Item {0} of {1}", a, b ].Value;""", 1)]
    [InlineData("""title="@T["Paid on {0:d}", date].Value" """, 1)]
    [InlineData("""T["Transaction: {0}", string.IsNullOrEmpty(t) ? T["(untitled)"].Value : t].Value""", 1)]
    [InlineData("T[\"Line {0}\",\r\n    model.Line].Value", 1)]
    [InlineData("""@T["Discount ({0})", name]""", 0)]
    [InlineData("""T.GetString("Discount ({0})", name).Value""", 0)]
    [InlineData("""data-template="@T["SKU {0}"].Value" """, 0)]
    [InlineData("""T["No placeholder", name].Value""", 0)]
    [InlineData("""T["Total: {0}", items["key"]]""", 0)]
    [InlineData("""S["Discount ({0})", name].Value""", 0)]
    public void FindFormattedValueReads_FlagsOnlyValueReadsWithArguments(string source, int expectedCount)
    {
        // Act
        var lines = FindFormattedValueReads(source);

        // Assert
        Assert.Equal(expectedCount, lines.Count);
    }

    /// <summary>
    /// Returns the 1-based line of every <c>T["...{N}...", args].Value</c> in the source. The scan walks the indexer
    /// with string and bracket awareness, so nested calls such as <c>T["(untitled)"].Value</c> or <c>items["key"]</c>
    /// inside the arguments neither end the indexer early nor count on their own.
    /// </summary>
    private static List<int> FindFormattedValueReads(string source, string localizer = "T")
        => FindIndexers(source, (hasArguments, readsValue, _) => hasArguments && readsValue, localizer);

    /// <summary>
    /// Returns every name an <c>IHtmlLocalizer</c> is declared under in a set of source files (a field, a property, a
    /// parameter or a view's <c>@inject</c>), and <c>H</c>, the conventional name, which a base class outside them may
    /// declare.
    /// </summary>
    private static HashSet<string> HtmlLocalizerNames(IEnumerable<string> sources)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "H" };

        foreach (var source in sources)
        {
            foreach (Match match in _htmlLocalizerDeclarationRegex.Matches(source))
            {
                names.Add(match.Groups["name"].Value);
            }
        }

        return names;
    }

    /// <summary>
    /// Returns, as <c>line (localizer)</c>, every <c>L["...{N}...", args].Value</c> in a source file, for each of the
    /// names an <c>IHtmlLocalizer</c> is declared under.
    /// </summary>
    private static List<string> FindHtmlValueReads(string source, IEnumerable<string> localizers)
        => localizers
            .SelectMany(localizer => FindFormattedValueReads(source, localizer).Select(line => $"{line} ({localizer})"))
            .ToList();

    /// <summary>
    /// Returns the 1-based line of every <c>@T["...{N}..."]</c> or <c>@(T["...{N}..."])</c> with no arguments: written
    /// to the page as it is. One handed to a helper, which may read its <c>.Value</c>, is left alone.
    /// </summary>
    private static List<int> FindPlaceholdersWithoutArguments(string source)
        => FindIndexers(source, (hasArguments, readsValue, written) => !hasArguments && !readsValue && written);

    private static List<int> FindIndexers(string source, Func<bool, bool, bool, bool> matches, string localizer = "T")
    {
        var lines = new List<int>();
        var opening = localizer + "[";

        for (var index = source.IndexOf(opening, StringComparison.Ordinal); index >= 0; index = source.IndexOf(opening, index + 1, StringComparison.Ordinal))
        {
            if (index > 0 && (char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_' || source[index - 1] == '.'))
            {
                continue;
            }

            var position = SkipWhitespace(source, index + opening.Length);

            if (!TryReadStringLiteral(source, ref position, out var name) || !_placeholderRegex.IsMatch(name))
            {
                continue;
            }

            if (!TryFindIndexerEnd(source, position, out var closingBracket, out var hasArguments))
            {
                continue;
            }

            var afterIndexer = SkipWhitespace(source, closingBracket + 1);
            var readsValue = string.CompareOrdinal(source, afterIndexer, ".Value", 0, 6) == 0
                && (afterIndexer + 6 >= source.Length || !char.IsLetterOrDigit(source[afterIndexer + 6]));

            var written = index > 0 && (source[index - 1] == '@' || index > 1 && source[index - 1] == '(' && source[index - 2] == '@');

            if (matches(hasArguments, readsValue, written))
            {
                lines.Add(CountLine(source, index));
            }
        }

        return lines;
    }

    private static bool TryReadStringLiteral(string source, ref int position, out string value)
    {
        value = string.Empty;
        var verbatim = position < source.Length && source[position] == '@';
        var start = verbatim ? position + 1 : position;

        if (start >= source.Length || source[start] != '"')
        {
            return false;
        }

        var end = SkipStringLiteral(source, start, verbatim);

        if (end < 0)
        {
            return false;
        }

        value = source[(start + 1)..(end - 1)];
        position = end;

        return true;
    }

    // Returns the index just past the closing quote, or -1 when the literal is not closed.
    private static int SkipStringLiteral(string source, int openingQuote, bool verbatim)
    {
        for (var i = openingQuote + 1; i < source.Length; i++)
        {
            if (verbatim)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    return i + 1;
                }
            }
            else if (source[i] == '\\')
            {
                i++;
            }
            else if (source[i] == '"')
            {
                return i + 1;
            }
            else if (source[i] == '\n')
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool TryFindIndexerEnd(string source, int position, out int closingBracket, out bool hasArguments)
    {
        closingBracket = -1;
        hasArguments = false;
        var depth = 0;

        for (var i = position; i < source.Length; i++)
        {
            var current = source[i];

            if (current == '"' || (current == '@' && i + 1 < source.Length && source[i + 1] == '"'))
            {
                var verbatim = current == '@';
                var end = SkipStringLiteral(source, verbatim ? i + 1 : i, verbatim);

                if (end < 0)
                {
                    return false;
                }

                i = end - 1;
            }
            else if (current == '\'')
            {
                // A char literal such as ',' or ']' must not be read as syntax.
                var end = source.IndexOf('\'', i + (i + 1 < source.Length && source[i + 1] == '\\' ? 3 : 2));

                if (end < 0)
                {
                    return false;
                }

                i = end;
            }
            else if (current is '(' or '[' or '{')
            {
                depth++;
            }
            else if (current is ')' or '}')
            {
                depth--;
            }
            else if (current == ']')
            {
                if (depth == 0)
                {
                    closingBracket = i;

                    return true;
                }

                depth--;
            }
            else if (current == ',' && depth == 0)
            {
                hasArguments = true;
            }
        }

        return false;
    }

    private static int SkipWhitespace(string source, int position)
    {
        while (position < source.Length && char.IsWhiteSpace(source[position]))
        {
            position++;
        }

        return position;
    }

    private static int CountLine(string source, int index)
    {
        var line = 1;

        for (var i = 0; i < index; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static bool IsBuildOutput(string fullPath)
    {
        var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
