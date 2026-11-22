using System.Reflection;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// Everything a question type does goes through its handler: no code outside a type's own files tests a type name, and
/// every registered type has its respondent and review views and its settings editor.
/// </summary>
public sealed class SurveyControlArchitectureTests
{
    private static readonly string[] _surveyProjects =
    [
        Path.Combine("src", "Abstractions", "CrestApps.OrchardCore.Surveys.Abstractions"),
        Path.Combine("src", "Core", "CrestApps.OrchardCore.Surveys.Core"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Surveys"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Surveys.AI"),
    ];

    private static readonly HashSet<string> _scannedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".cshtml", ".js", ".liquid" };

    private static readonly HashSet<string> _skippedFolders = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", "wwwroot" };

    // The constant names of SurveyControlNames, mapped to their values.
    private static readonly Dictionary<string, string> _constants = typeof(SurveyControlNames)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(field => field.Name, field => (string)field.GetValue(null), StringComparer.Ordinal);

    private static readonly Regex[] _comparisons = BuildComparisons();

    [Fact]
    public void SurveysSources_NeverTestAControlTypeName_OutsideTheTypesOwnFiles()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var project in _surveyProjects)
        {
            var directory = Path.Combine(root, project);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in EnumerateSources(directory))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (relative.EndsWith("/SurveyControlNames.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);

                for (var index = 0; index < lines.Length; index++)
                {
                    foreach (var name in FindTestedNames(lines[index]))
                    {
                        if (name is null || !IsOwnFile(relative, name))
                        {
                            offenders.Add($"{relative}:{index + 1}: {lines[index].Trim()}");
                        }
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, "These lines test a question type name instead of asking its handler or its capabilities: " + string.Join(Environment.NewLine, offenders.Distinct()));
    }

    [Theory]
    [InlineData("if (question.ControlType == SurveyControlNames.Matrix)", "Matrix")]
    [InlineData("if (question.ControlType != SurveyControlNames.Matrix)", "Matrix")]
    [InlineData("SurveyControlNames.Matrix == question.ControlType", "Matrix")]
    [InlineData("if (type is SurveyControlNames.Slider or SurveyControlNames.Ranking)", "Slider")]
    [InlineData("if (type is not SurveyControlNames.Slider)", "Slider")]
    [InlineData("case SurveyControlNames.Rating:", "Rating")]
    [InlineData("SurveyControlNames.Rating => 5,", "Rating")]
    [InlineData("string.Equals(type, SurveyControlNames.TextBlock, StringComparison.Ordinal)", "TextBlock")]
    [InlineData("names.Contains(SurveyControlNames.Date)", "Date")]
    [InlineData("_fileTypes.Contains(question.ControlType)", null)]
    [InlineData("if (type == \"SingleChoice\")", "SingleChoice")]
    [InlineData("if (type === 'Slider') {", "Slider")]
    [InlineData("case \"Matrix\":", "Matrix")]
    [InlineData("\"Hidden\" => 0,", "Hidden")]
    [InlineData("types.includes('Signature')", "Signature")]
    public void FindTestedNames_CatchesEveryComparisonForm(string line, string expected)
    {
        var names = FindTestedNames(line).ToList();

        Assert.Contains(expected, names);
    }

    [Theory]
    [InlineData("public override string ControlType => SurveyControlNames.Rating;")]
    [InlineData("services.AddSurveyControl<RatingControlHandler>(SurveyControlNames.Rating, descriptor =>")]
    [InlineData("var title = \"Number of answers\";")]
    public void FindTestedNames_IgnoresNamesThatAreNotCompared(string line)
        => Assert.Empty(FindTestedNames(line));

    [Fact]
    public void RegisteredControls_HaveRespondReviewViewsAndASettingsEditor()
    {
        using var services = ControlsTestServices.Build();
        var registry = ControlsTestServices.Registry(services);
        var views = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Surveys", "Views");
        var module = typeof(CrestApps.OrchardCore.Surveys.ControlsStartup).Assembly;
        var missing = new List<string>();

        foreach (var descriptor in registry.GetControls())
        {
            foreach (var displayType in new[] { "Respond", "Review" })
            {
                // Built-in names have no dot, so either file name binds; SurveyControl-{Name}.{DisplayType} is the
                // form documented for every name.
                if (!File.Exists(Path.Combine(views, $"SurveyControl.{displayType}-{descriptor.Name}.cshtml"))
                    && !File.Exists(Path.Combine(views, $"SurveyControl-{descriptor.Name}.{displayType}.cshtml")))
                {
                    missing.Add($"SurveyControl-{descriptor.Name}.{displayType}.cshtml");
                }
            }

            if (descriptor.SettingsType is not null && module.GetType($"CrestApps.OrchardCore.Surveys.Drivers.Controls.{descriptor.Name}SettingsDisplayDriver") is null)
            {
                missing.Add($"{descriptor.Name}SettingsDisplayDriver");
            }

            if (descriptor.SettingsType is not null && !File.Exists(Path.Combine(views, $"SurveyControlSettings-{descriptor.Name}.Edit.cshtml")))
            {
                missing.Add($"SurveyControlSettings-{descriptor.Name}.Edit.cshtml");
            }
        }

        Assert.True(missing.Count == 0, "Missing: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Finds the type names a line compares something with. A <see langword="null"/> name is a test against a list of
    /// names, such as <c>names.Contains(question.ControlType)</c>.
    /// </summary>
    private static IEnumerable<string> FindTestedNames(string line)
    {
        foreach (var comparison in _comparisons)
        {
            foreach (Match match in comparison.Matches(line))
            {
                if (match.Groups["list"].Success)
                {
                    yield return null;

                    continue;
                }

                var constant = match.Groups["constant"];

                if (constant.Success)
                {
                    if (_constants.TryGetValue(constant.Value, out var value))
                    {
                        yield return value;
                    }

                    continue;
                }

                yield return match.Groups["literal"].Value;
            }
        }
    }

    /// <summary>
    /// Gets whether a file belongs to the type it tests: a file in the type's folder (<c>Controls/{Name}/</c>), a file
    /// named after it (<c>{Name}SettingsDisplayDriver.cs</c>, <c>{Name}Analyzer.cs</c>), or one of its views
    /// (<c>SurveyControl.Respond-{Name}.cshtml</c>, <c>SurveyControlSettings-{Name}.Edit.cshtml</c>).
    /// </summary>
    private static bool IsOwnFile(string relative, string name)
    {
        var fileName = Path.GetFileName(relative);

        return relative.Contains($"/Controls/{name}/", StringComparison.Ordinal)
            || fileName.StartsWith(name, StringComparison.Ordinal)
            || fileName.Contains($"-{name}.", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (_scannedExtensions.Contains(Path.GetExtension(file)))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (_skippedFolders.Contains(Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in EnumerateSources(child))
            {
                yield return file;
            }
        }
    }

    private static Regex[] BuildComparisons()
    {
        var literals = string.Join('|', _constants.Values.Distinct(StringComparer.Ordinal).Select(Regex.Escape));
        const string Constant = @"SurveyControlNames\.(?<constant>\w+)";
        var literal = $@"[""'](?<literal>{literals})[""']";

        string[] patterns =
        [
            // C#: ==, !=, is, is not, or, and, case, switch arms, Equals and Contains.
            $@"(?:==|!=)\s*{Constant}",
            $@"{Constant}\s*(?:==|!=|=>|\bor\b|\band\b)",
            $@"\b(?:is|is\s+not|or|and|case)\s+{Constant}",
            $@"Equals\([^)]*{Constant}",
            $@"\.Contains\(\s*{Constant}",
            @"\.Contains\(\s*(?<list>[\w.?]*ControlType)\b",

            // Literal names, in C#, Razor and scripts.
            $@"(?:===?|!==?)\s*{literal}",
            $@"{literal}\s*(?:===?|!==?|=>)",
            $@"\b(?:case|is|is\s+not|or)\s+{literal}",
            $@"Equals\([^)]*{literal}",
            $@"\.(?:Contains|includes)\(\s*{literal}",
        ];

        return patterns.Select(pattern => new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1))).ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
