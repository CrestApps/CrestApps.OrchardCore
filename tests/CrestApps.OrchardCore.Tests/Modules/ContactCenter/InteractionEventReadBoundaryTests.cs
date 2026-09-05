using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Keeps the durable event log behind the store that converts it. A stored payload written by an earlier
/// release deserializes into today's type without complaint, substituting a default for whatever moved, so the
/// stored schema version is read and the payload converted on the way out of <c>InteractionEventStore</c>.
/// Code that queries the event log straight off the session bypasses that conversion and cannot be discovered
/// by the reflective coverage gate, which only knows about the store's own read paths. Reporting did exactly
/// that until this gate existed.
/// </summary>
public sealed partial class InteractionEventReadBoundaryTests
{
    private const string StoreFileName = "InteractionEventStore.cs";

    [Fact]
    public void NoProductionSourceOutsideTheStore_ReadsTheEventLogDirectlyFromTheSession()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var file in EnumerateProductionSources())
        {
            var source = File.ReadAllText(file);

            if (DirectEventQuery().IsMatch(source))
            {
                violations.Add(
                    $"{file} queries InteractionEvent directly. Read it through IInteractionEventStore, which converts a payload written by an earlier release before the caller deserializes it.");
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void TheStore_IsPresentAndIsTheOneThatQueriesTheEventLog()
    {
        // Arrange
        var storeFiles = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), StoreFileName, SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .ToArray();

        // Act
        var source = Assert.Single(storeFiles);

        // Assert
        // Without this the first assertion would pass by matching nothing at all, which is what it would do if
        // the query shape it looks for were ever spelled differently.
        Assert.Matches(DirectEventQuery(), File.ReadAllText(source));
    }

    private static IEnumerable<string> EnumerateProductionSources()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(file) || string.Equals(Path.GetFileName(file), StoreFileName, StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }

    private static bool IsGenerated(string file)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;

        return directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"Query<\s*InteractionEvent\s*[,>]")]
    private static partial Regex DirectEventQuery();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
