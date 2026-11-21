using System.Reflection;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// Everything a question type does goes through its handler: no code outside a control's own folders tests a type
/// name, and every registered type has its respondent and review views and its settings editor.
/// </summary>
public sealed partial class SurveyControlArchitectureTests
{
    private static readonly string[] _builtInNames = typeof(SurveyControlNames)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (string)field.GetValue(null))
        .ToArray();

    // A comparison with a type name written as a literal, such as == "SingleChoice" or case "Matrix".
    private static readonly Regex _literalComparison = new(
        $@"(==\s*|case\s+|Equals\([^)]*)""({string.Join('|', _builtInNames.Select(Regex.Escape))})""",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void SurveysSources_OutsideControlFolders_NeverTestAControlTypeName()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (!relative.Contains("/CrestApps.OrchardCore.Surveys", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/Controls/", StringComparison.Ordinal)
                || relative.EndsWith("/SurveyControlNames.cs", StringComparison.Ordinal)
                || relative.EndsWith("/SurveysControlsServiceCollectionExtensions.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (NameComparison().IsMatch(text) || _literalComparison.IsMatch(text))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(offenders.Count == 0, "These files test a question type name instead of asking its handler: " + string.Join(", ", offenders));
    }

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
                if (!File.Exists(Path.Combine(views, $"SurveyControl.{displayType}-{descriptor.Name}.cshtml")))
                {
                    missing.Add($"SurveyControl.{displayType}-{descriptor.Name}.cshtml");
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex(@"case\s+SurveyControlNames\.|==\s*SurveyControlNames\.|SurveyControlNames\.\w+\s*==|Equals\([^)]*SurveyControlNames\.")]
    private static partial Regex NameComparison();
}
