using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A deployment plan page renders every registered step through its <c>{Step}_Thumbnail</c> shape (in the Add Step
/// dialog) and its <c>{Step}_Summary</c> shape (in the plan's list). A step registered without a display driver of its
/// own, with <c>AddDeployment&lt;TSource, TStep&gt;()</c> or with <c>SummaryDeploymentStepDisplayDriver&lt;TStep&gt;</c>,
/// relies on the module to provide both views. When one is missing, Orchard Core throws "The shape type '...' is not
/// found" and every deployment plan page of the tenant fails, not just the plans that use the step. This guard fails
/// when such a step has no <c>{Step}.Summary.cshtml</c> and <c>{Step}.Thumbnail.cshtml</c> view under a module.
/// </summary>
public sealed class DeploymentStepViewsArchitectureTests
{
    private static readonly Regex _viewlessStepRegex = new(
        @"\.AddDeployment\s*<\s*[\w.]+\s*,\s*(?:global::)?(?<step>[\w.]+)\s*(?:,\s*(?:[\w.]+\.)?SummaryDeploymentStepDisplayDriver\s*<\s*[\w.]+\s*>\s*)?>\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] _requiredViewSuffixes =
    [
        "Summary",
        "Thumbnail",
    ];

    private static readonly HashSet<string> _skippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "node_modules",
        ".git",
        "wwwroot",
    };

    [Fact]
    public void DeploymentStepsWithoutTheirOwnDriver_HaveSummaryAndThumbnailViews()
    {
        // Arrange
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var stepNames = FindViewlessStepNames(sourceRoot);
        var viewNames = FindViewNames(sourceRoot);

        // Act
        var missing = stepNames
            .SelectMany(stepName => _requiredViewSuffixes.Select(suffix => $"{stepName}.{suffix}.cshtml"))
            .Where(viewName => !viewNames.Contains(viewName))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.True(
            stepNames.Count > 0,
            "No AddDeployment<TSource, TStep> registrations without a driver were found under src, so the guard would pass vacuously.");
        Assert.True(
            missing.Length == 0,
            "These deployment steps have no display driver of their own and no view, so every deployment plan page " +
            $"throws \"The shape type ... is not found\". Add the views to the module that registers the step: {string.Join(", ", missing)}.");
    }

    private static HashSet<string> FindViewlessStepNames(string sourceRoot)
    {
        var stepNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullPath in EnumerateSourceFiles(sourceRoot, "*.cs"))
        {
            foreach (Match match in _viewlessStepRegex.Matches(File.ReadAllText(fullPath)))
            {
                var qualifiedName = match.Groups["step"].Value;

                // The shape type is built from the CLR type name, so drop any namespace qualifier.
                stepNames.Add(qualifiedName[(qualifiedName.LastIndexOf('.') + 1)..]);
            }
        }

        return stepNames;
    }

    private static HashSet<string> FindViewNames(string sourceRoot)
    {
        var viewNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullPath in EnumerateSourceFiles(sourceRoot, "*.cshtml"))
        {
            if (fullPath.Replace(Path.DirectorySeparatorChar, '/').Contains("/Views/", StringComparison.Ordinal))
            {
                viewNames.Add(Path.GetFileName(fullPath));
            }
        }

        return viewNames;
    }

    // Walks the source tree without descending into build output or package folders.
    private static IEnumerable<string> EnumerateSourceFiles(string directory, string searchPattern)
    {
        foreach (var filePath in Directory.EnumerateFiles(directory, searchPattern))
        {
            yield return filePath;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            if (_skippedDirectoryNames.Contains(Path.GetFileName(childDirectory)))
            {
                continue;
            }

            foreach (var filePath in EnumerateSourceFiles(childDirectory, searchPattern))
            {
                yield return filePath;
            }
        }
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
