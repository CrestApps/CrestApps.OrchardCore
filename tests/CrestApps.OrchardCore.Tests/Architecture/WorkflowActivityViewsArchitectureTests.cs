using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// The workflow editor renders every registered activity through its <c>{Activity}_Fields_Thumbnail</c> shape (in the
/// Add Activity picker) and its <c>{Activity}_Fields_Design</c> shape (on the canvas). When either view is missing,
/// Orchard Core throws "The shape type '...' is not found" and the editor page for every workflow type fails, not just
/// the ones that use the activity. Razor views are compiled at run time, so a build does not catch the gap. This guard
/// fails when an activity registered with <c>AddActivity&lt;TActivity, TDriver&gt;</c> has no matching
/// <c>Views/Items/{TActivity}.Fields.Thumbnail.cshtml</c> and <c>Views/Items/{TActivity}.Fields.Design.cshtml</c>.
/// </summary>
public sealed class WorkflowActivityViewsArchitectureTests
{
    private static readonly Regex _addActivityRegex = new(
        @"\.AddActivity\s*<\s*(?:global::)?(?<activity>[\w.]+)\s*,",
        RegexOptions.Compiled);

    private static readonly string[] _requiredShapeSuffixes =
    [
        "Fields.Thumbnail",
        "Fields.Design",
    ];

    private static readonly HashSet<string> _skippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "node_modules",
        ".git",
    };

    [Fact]
    public void RegisteredWorkflowActivities_HaveThumbnailAndDesignViews()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var activityNames = FindRegisteredActivityNames(sourceRoot);
        var itemViewNames = FindItemViewNames(sourceRoot);

        // Act
        var missing = activityNames
            .SelectMany(activityName => _requiredShapeSuffixes
                .Select(suffix => $"{activityName}.{suffix}.cshtml"))
            .Where(viewName => !itemViewNames.Contains(viewName))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.True(
            activityNames.Count > 0,
            "No AddActivity<TActivity, TDriver> registrations were found under src, so the guard would pass vacuously.");
        Assert.True(
            missing.Length == 0,
            "These workflow activities are registered with AddActivity but have no view under a module's Views/Items " +
            "folder, so opening any workflow editor throws \"The shape type ... is not found\". Add the views to the " +
            $"module that registers the activity: {string.Join(", ", missing)}.");
    }

    private static HashSet<string> FindRegisteredActivityNames(string sourceRoot)
    {
        var activityNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullPath in EnumerateSourceFiles(sourceRoot, "*.cs"))
        {
            foreach (Match match in _addActivityRegex.Matches(File.ReadAllText(fullPath)))
            {
                var qualifiedName = match.Groups["activity"].Value;

                // The shape type is built from the CLR type name, so drop any namespace qualifier.
                activityNames.Add(qualifiedName[(qualifiedName.LastIndexOf('.') + 1)..]);
            }
        }

        return activityNames;
    }

    private static HashSet<string> FindItemViewNames(string sourceRoot)
    {
        var viewNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullPath in EnumerateSourceFiles(sourceRoot, "*.cshtml"))
        {
            var directory = Path.GetDirectoryName(fullPath).Replace(Path.DirectorySeparatorChar, '/');

            if (directory.EndsWith("/Views/Items", StringComparison.Ordinal))
            {
                viewNames.Add(Path.GetFileName(fullPath));
            }
        }

        return viewNames;
    }

    // Walks the source tree without descending into build output or package folders, which hold most of the files
    // under src and would make the scan take minutes.
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
