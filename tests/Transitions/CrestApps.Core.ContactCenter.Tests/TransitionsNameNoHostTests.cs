using System.Text.RegularExpressions;

namespace CrestApps.Core.ContactCenter.Tests;

/// <summary>
/// Pins the one thing that makes these projects portable: they do not name the host they are leaving.
/// </summary>
/// <remarks>
/// <para>
/// A project under a <c>Transitions</c> folder is the Contact Center Suite as it will exist once it is
/// extracted. A reference to the host that creeps into one compiles perfectly well here, because the host
/// is right there in the same solution, and is discovered only when the move is attempted - which is the
/// single worst moment to discover it, because by then it is a pile of unrelated work blocking a release.
/// </para>
/// <para>
/// Continuous integration checks the same thing, but a rule that only runs there is a rule that is found
/// broken after the change is pushed. This one fails on the machine that wrote the reference.
/// </para>
/// </remarks>
public sealed class TransitionsNameNoHostTests
{
    private static readonly string[] _folders =
    [
        Path.Combine("src", "Abstractions", "Transitions"),
        Path.Combine("src", "Core", "Transitions"),
        Path.Combine("tests", "Transitions"),
    ];

    private static readonly string[] _extensions = [".cs", ".csproj", ".props"];

    [Fact]
    public void NothingUnderTransitions_NamesTheHost()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var scanned = 0;

        // Act
        var offenders = new List<string>();

        foreach (var folder in _folders)
        {
            var directory = Path.Combine(repositoryRoot, folder);

            Assert.True(Directory.Exists(directory), $"'{folder}' is not where this rule looks for it.");

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!_extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;

                var text = File.ReadAllText(file);

                // Only outside a comment. Naming the host in prose - saying which product a seam exists for -
                // is the sort of thing these files should say; depending on it is not.
                if (Regex.IsMatch(StripComments(text), @"\bOrchardCore\b"))
                {
                    offenders.Add(Path.GetRelativePath(repositoryRoot, file));
                }
            }
        }

        // Assert
        Assert.True(scanned > 0, "This rule read no files at all, so it proves nothing.");

        Assert.True(
            offenders.Count == 0,
            "These files are staged to leave this repository and still name the host they are leaving, which " +
            "will not compile once they do: " + string.Join(", ", offenders.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Removes comments so a mention of the host in prose is not read as a dependency on it.
    /// </summary>
    /// <param name="text">The file text.</param>
    /// <returns>The text with its comments blanked out.</returns>
    private static string StripComments(string text)
        => Regex.Replace(text, @"//.*?$|/\*.*?\*/|<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.Multiline);

    /// <summary>
    /// Walks up to the repository root.
    /// </summary>
    /// <remarks>
    /// Found by the central package file rather than by the solution, whose name contains the very word this
    /// rule searches for - which would make the rule report itself.
    /// </remarks>
    /// <returns>The repository root.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "The repository root was not found, so this rule reads nothing.");

        return directory.FullName;
    }
}
