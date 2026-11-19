namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A migration asks for its resumable step directly: the request is a row written in the migration's own transaction,
/// so it commits with the migration. Through <c>ShellScope.AddDeferredTask</c> it would run after the commit, where a
/// failure is swallowed and the step is never asked for.
/// </summary>
public sealed class ResumableStepRequestTests
{
    [Fact]
    public void NoMigration_RequestsAResumableStep_ThroughADeferredTask()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var migrations = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal));

        // Act
        var deferred = migrations
            .Where(path => File.ReadAllText(path).Contains("GetRequiredService<ResumableSteps>()", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        // Assert
        Assert.True(deferred.Count == 0, "Request the step through an injected ResumableSteps instead: " + string.Join(", ", deferred));
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
