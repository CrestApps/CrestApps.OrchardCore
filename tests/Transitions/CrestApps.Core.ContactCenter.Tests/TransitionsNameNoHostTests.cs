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
    /// <summary>
    /// The host these projects are leaving, assembled rather than written out.
    /// </summary>
    /// <remarks>
    /// Written this way so that searching for it does not depend on how this file happens to spell it, and so
    /// that this file is not the reason the rule fails.
    /// </remarks>
    private static readonly string _host = string.Concat("Orchard", "Core");

    private static readonly string[] _folders =
    [
        Path.Combine("src", "Abstractions", "Transitions"),
        Path.Combine("src", "Core", "Transitions"),
        Path.Combine("tests", "Transitions"),
    ];

    private static readonly string[] _extensions = [".cs", ".csproj", ".props"];

    /// <summary>
    /// The one file under these folders that is allowed to name the host: this one, which is the rule.
    /// </summary>
    /// <remarks>
    /// Excluded by name rather than left to chance. The continuous-integration copy of this rule greps for the
    /// plain word and would otherwise report the rule itself, which reads like a bug to whoever finds it.
    /// </remarks>
    private const string ThisRule = "TransitionsNameNoHostTests.cs";

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
                if (string.Equals(Path.GetFileName(file), ThisRule, StringComparison.Ordinal) ||
                    !_extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;

                var text = File.ReadAllText(file);

                // Only outside a comment. Naming the host in prose - saying which product a seam exists for -
                // is the sort of thing these files should say; depending on it is not.
                if (StripComments(text).Contains(_host, StringComparison.Ordinal))
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
    /// Removes comments and string literals, so that mentioning the host is not read as depending on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Comments are removed because saying which host a seam exists for is the sort of thing these files should
    /// say.
    /// </para>
    /// <para>
    /// String literals are removed because some of them cannot be renamed. Two are data-protection purposes -
    /// the key derivation reads them - so changing their text does not rename anything, it makes every user
    /// token and every stored recording encrypted under the old text permanently unreadable. Others are feature
    /// identifiers a host already has written into its database. These travel with the code precisely because
    /// they must not change.
    /// </para>
    /// <para>
    /// The cost is that a dependency expressed as a string, such as a type resolved by name at runtime, is not
    /// caught here. That is the narrower risk: it fails loudly the first time it runs, where a renamed
    /// derivation purpose fails silently and unrecoverably.
    /// </para>
    /// </remarks>
    /// <param name="text">The file text.</param>
    /// <returns>The text with its comments and string literals blanked out.</returns>
    private static string StripComments(string text)
    {
        var withoutComments = Regex.Replace(
            text,
            @"//.*?$|/\*.*?\*/|<!--.*?-->",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.Multiline);

        return Regex.Replace(withoutComments, "\"(?:[^\"\\\\\\r\\n]|\\\\.)*\"", "\"\"");
    }

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
