using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// In a Razor view <c>T</c> is an <c>IViewLocalizer</c>, and <c>T["Discount ({0})", name]</c> returns a
/// <c>LocalizedHtmlString</c>. Its arguments are applied only when the string is written as HTML content; its
/// <c>.Value</c> is the unformatted translation, so <c>T["Discount ({0})", name].Value</c> shows the user a literal
/// "{0}". This guard fails when a view under <c>src/Modules</c> reads <c>.Value</c> from a localized string that has
/// format placeholders and arguments. Write the <c>LocalizedHtmlString</c> itself where it is content, or use
/// <c>T.GetString("text {0}", arg).Value</c> where a plain string is needed (an attribute or a C# variable).
/// </summary>
public sealed class ViewLocalizerFormatArgumentsArchitectureTests
{
    private static readonly Regex _placeholderRegex = new(@"\{\d+[,:}]", RegexOptions.Compiled);

    [Fact]
    public void Views_DoNotReadTheUnformattedValueOfALocalizedStringWithArguments()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        var violations = new List<string>();
        var scannedViewCount = 0;

        // Act
        foreach (var fullPath in Directory.EnumerateFiles(modulesRoot, "*.cshtml", SearchOption.AllDirectories))
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
    private static List<int> FindFormattedValueReads(string source)
    {
        var lines = new List<int>();

        for (var index = source.IndexOf("T[", StringComparison.Ordinal); index >= 0; index = source.IndexOf("T[", index + 1, StringComparison.Ordinal))
        {
            if (index > 0 && (char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_' || source[index - 1] == '.'))
            {
                continue;
            }

            var position = SkipWhitespace(source, index + 2);

            if (!TryReadStringLiteral(source, ref position, out var name) || !_placeholderRegex.IsMatch(name))
            {
                continue;
            }

            if (!TryFindIndexerEnd(source, position, out var closingBracket, out var hasArguments) || !hasArguments)
            {
                continue;
            }

            var afterIndexer = SkipWhitespace(source, closingBracket + 1);

            if (string.CompareOrdinal(source, afterIndexer, ".Value", 0, 6) == 0
                && (afterIndexer + 6 >= source.Length || !char.IsLetterOrDigit(source[afterIndexer + 6])))
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
