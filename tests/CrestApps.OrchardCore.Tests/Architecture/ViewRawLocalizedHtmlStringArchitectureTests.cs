namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A <c>LocalizedHtmlString</c> writes its value as HTML, unencoded, and through <c>string.Format</c>. A view that falls
/// back to <c>new LocalizedHtmlString(value, value)</c> for a stored value it has no translation for (a survey kind, a
/// path's set title) writes that value as markup, which is stored XSS when anyone can set it (R11-M1), and a brace in it
/// throws while the page renders. This guard fails when a view under <c>src/Modules</c> builds a
/// <c>LocalizedHtmlString</c> whose value is not a string literal. Write a stored value as
/// <c>new HtmlString(Html.Encode(value))</c>, or as a plain string.
/// </summary>
public sealed class ViewRawLocalizedHtmlStringArchitectureTests
{
    private const string Constructor = "LocalizedHtmlString(";

    [Fact]
    public void Views_DoNotWriteAStoredValueAsALocalizedHtmlString()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        var violations = new List<string>();
        var scannedViewCount = 0;

        // Act
        foreach (var fullPath in SourceTreeFiles.Enumerate(modulesRoot, "*.cshtml", SourceTreeFiles.IsBuildOutputDirectory))
        {
            scannedViewCount++;
            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');

            foreach (var line in FindRawValues(File.ReadAllText(fullPath)))
            {
                violations.Add($"{relativePath}:{line}");
            }
        }

        // Assert
        Assert.True(scannedViewCount > 0, "No .cshtml views were found under src/Modules, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These views write a stored value as a LocalizedHtmlString, which is neither encoded nor safe from braces. Write new HtmlString(Html.Encode(value)) instead: " +
            $"{string.Join(", ", violations)}.");
    }

    [Theory]
    [InlineData("_ => new LocalizedHtmlString(survey.Kind ?? string.Empty, survey.Kind ?? string.Empty),", 1)]
    [InlineData("@(x ? T[\"A\"] : new Microsoft.AspNetCore.Mvc.Localization.LocalizedHtmlString(set.Title, set.Title))", 1)]
    [InlineData("_ => new LocalizedHtmlString(survey.Kind ?? string.Empty, Html.Encode(survey.Kind ?? string.Empty)),", 1)]
    [InlineData("_ => (IHtmlContent)new HtmlString(Html.Encode(survey.Kind ?? string.Empty)),", 0)]
    [InlineData("var empty = new LocalizedHtmlString(\"None\", \"None\");", 0)]
    public void FindRawValues_FlagsEveryValueThatIsNotALiteral(string source, int expected)
    {
        // Act
        var lines = FindRawValues(source);

        // Assert
        Assert.Equal(expected, lines.Count);
    }

    private static List<int> FindRawValues(string source)
    {
        var lines = new List<int>();
        var index = 0;

        while ((index = source.IndexOf(Constructor, index, StringComparison.Ordinal)) >= 0)
        {
            var start = index + Constructor.Length;
            var value = SecondArgument(source, start);

            if (value is not null && !value.StartsWith('"'))
            {
                lines.Add(source.AsSpan(0, index).Count('\n') + 1);
            }

            index = start;
        }

        return lines;
    }

    // The text of the second argument of the call whose arguments start at start, or null when it has none.
    private static string SecondArgument(string source, int start)
    {
        var depth = 0;
        var inString = false;
        var argumentStart = -1;

        for (var i = start; i < source.Length; i++)
        {
            var character = source[i];

            if (inString)
            {
                if (character == '\\')
                {
                    i++;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}' when depth > 0:
                    depth--;
                    break;
                case ')':
                    return argumentStart < 0 ? null : source[argumentStart..i].Trim();
                case ',' when depth == 0:
                    if (argumentStart >= 0)
                    {
                        return source[argumentStart..i].Trim();
                    }

                    argumentStart = i + 1;
                    break;
            }
        }

        return null;
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
