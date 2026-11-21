namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// The admin menu has no Configuration item: a module's site settings page goes under the top-level Settings item
/// (<c>builder.Add(S["Settings"], ...)</c>). Only the legacy menu format of Orchard Core still nests Settings under
/// Configuration, so a navigation provider may name Configuration only inside a branch guarded by
/// <c>OrchardCoreHelpers.UseLegacyAdminMenuFormat()</c>.
/// </summary>
public sealed class AdminMenuConfigurationArchitectureTests
{
    [Fact]
    public void AdminMenus_DoNotAddAConfigurationItem()
    {
        // Arrange
        var modules = Path.Combine(FindRepositoryRoot(), "src", "Modules");
        var offenders = new List<string>();

        // Act
        foreach (var file in Directory.EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');

            if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (text.Contains("S[\"Configuration\"]", StringComparison.Ordinal)
                && !text.Contains("UseLegacyAdminMenuFormat", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(modules, file));
            }
        }

        // Assert
        Assert.True(
            offenders.Count == 0,
            "These navigation providers add a Configuration admin menu item. Put site settings under the top-level Settings item instead: " + string.Join(", ", offenders));
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
