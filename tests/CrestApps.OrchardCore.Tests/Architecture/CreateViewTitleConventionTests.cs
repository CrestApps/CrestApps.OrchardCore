using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Create pages were titled <c>T["New '{0}' Disposition", Model.DisplayName]</c>, and the display name passed in
/// was the entity's own name, so the page read "New 'Disposition' Disposition". They now use a fixed title such as
/// "New Disposition". This guard fails when a Create view goes back to quoting the display name in front of a noun,
/// except for the views where <c>{0}</c> is a real provider or type name the user picked (a deployment, provider
/// connection, data source, MCP connection, channel endpoint or inventory load source).
/// </summary>
public sealed class CreateViewTitleConventionTests
{
    private static readonly Regex _quotedDisplayNameTitleRegex = new(
        @"T\[\s*""New '\{0\}' [^""]+""\s*,\s*Model\.DisplayName\s*\]",
        RegexOptions.Compiled);

    // Views whose {0} is the provider or source type the user chose, so the quoted name is not a repeat of the noun.
    private static readonly HashSet<string> _allowedViews = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/Modules/CrestApps.OrchardCore.AI/Views/Deployments/Create.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI/Views/ProviderConnections/Create.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.DataSources/Views/DataSources/Create.cshtml",
        "src/Modules/CrestApps.OrchardCore.AI.Mcp/Views/Connections/Create.cshtml",
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Views/ChannelEndpoints/Create.cshtml",
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Views/ActivityBatches/Create.cshtml",
    };

    [Fact]
    public void CreateViews_DoNotRepeatTheEntityNameInAQuotedDisplayNameTitle()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var modulesRoot = Path.Combine(repositoryRoot, "src", "Modules");
        var violations = new List<string>();
        var scannedViewCount = 0;

        // Act
        foreach (var fullPath in Directory.EnumerateFiles(modulesRoot, "Create.cshtml", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(fullPath))
            {
                continue;
            }

            scannedViewCount++;

            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');

            if (_allowedViews.Contains(relativePath))
            {
                continue;
            }

            if (_quotedDisplayNameTitleRegex.IsMatch(File.ReadAllText(fullPath)))
            {
                violations.Add(relativePath);
            }
        }

        // Assert
        Assert.True(
            scannedViewCount > 0,
            "No Create.cshtml views were found under src/Modules, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These Create views title the page with the display name quoted in front of the entity noun, which reads " +
            "as \"New 'Disposition' Disposition\"; use a fixed title such as T[\"New Disposition\"]: " +
            $"{string.Join(", ", violations)}.");
    }

    [Fact]
    public void CreateViewTitleAllowList_OnlyNamesViewsThatExist()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var missing = _allowedViews
            .Where(relativePath => !File.Exists(Path.Combine(repositoryRoot, relativePath)))
            .ToArray();

        // Assert
        Assert.True(
            missing.Length == 0,
            $"The Create view title allow-list names views that no longer exist: {string.Join(", ", missing)}.");
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
