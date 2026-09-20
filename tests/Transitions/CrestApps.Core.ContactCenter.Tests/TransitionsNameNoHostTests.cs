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

    /// <summary>
    /// The <c>Transitions</c> folders this rule must find, whatever else it discovers.
    /// </summary>
    /// <remarks>
    /// The folders are discovered rather than listed, so a new one - <c>src/Modules/Transitions</c> is the
    /// obvious next - is governed the day it is created rather than the day somebody remembers this file.
    /// These three are asserted on top of that, because a discovery that quietly finds nothing proves nothing.
    /// </remarks>
    private static readonly string[] _requiredFolders =
    [
        Path.Combine("src", "Abstractions", "Transitions"),
        Path.Combine("src", "Core", "Transitions"),
        Path.Combine("tests", "Transitions"),
    ];

    /// <summary>
    /// The files this rule reads.
    /// </summary>
    /// <remarks>
    /// <c>.targets</c> is here with <c>.props</c> because MSBuild imports it the same way and it can declare a
    /// reference the same way. <c>.json</c> and the view extensions are here because a host type reached by
    /// name from a recipe, a resource manifest or a view is still a dependency on the host.
    /// </remarks>
    private static readonly string[] _extensions =
    [
        ".cs",
        ".csproj",
        ".props",
        ".targets",
        ".json",
        ".cshtml",
        ".razor",
    ];

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

        var folders = DiscoverTransitionFolders(repositoryRoot);

        foreach (var required in _requiredFolders)
        {
            Assert.Contains(
                Path.Combine(repositoryRoot, required),
                folders,
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var directory in folders)
        {
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
                if (Redact(file, text).Contains(_host, StringComparison.Ordinal))
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
    /// Pins that the rule above can see the one thing it exists to find.
    /// </summary>
    /// <remarks>
    /// A project states every dependency it has inside a quoted attribute value. Redacting a project file the
    /// way source is redacted therefore blanks every reference in it, and the rule passes a project that
    /// depends on the host outright. That is not a hypothetical: it is what this rule did until this test was
    /// written, and nothing else would have reported it.
    /// </remarks>
    /// <param name="extension">The extension of the file being redacted.</param>
    [Theory]
    [InlineData(".csproj")]
    [InlineData(".props")]
    public void ARuleThatReadsAProjectFile_SeesTheReferencesInIt(string extension)
    {
        // Arrange
        var project =
            "<Project>\n" +
            "  <ItemGroup>\n" +
            "    <PackageReference Include=\"" + _host + ".Abstractions\" />\n" +
            "    <ProjectReference Include=\"..\\CrestApps." + _host + ".Core\\CrestApps." + _host + ".Core.csproj\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n";

        // Act
        var redacted = Redact("Sample" + extension, project);

        // Assert
        Assert.Contains(_host, redacted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins that the reason source is redacted differently still holds.
    /// </summary>
    /// <remarks>
    /// A data-protection purpose and a stored feature identifier are values that must not be renamed, and both
    /// contain the host name. Source that carries one is not depending on the host.
    /// </remarks>
    [Fact]
    public void ARuleThatReadsSource_IgnoresAValueThatMerelyContainsTheHostName()
    {
        // Arrange
        var source =
            "namespace CrestApps.Core.Sample;\n" +
            "\n" +
            "public static class Purposes\n" +
            "{\n" +
            "    public const string Token = \"CrestApps." + _host + ".Telephony.UserToken\";\n" +
            "}\n";

        // Act
        var redacted = Redact("Sample.cs", source);

        // Assert
        Assert.DoesNotContain(_host, redacted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins that no extracted assembly references the host, however the reference was introduced.
    /// </summary>
    /// <remarks>
    /// The rule above is textual, so a reference that arrives through an imported build file, a path built
    /// from an MSBuild property, or a package that itself references the host is invisible to it. What the
    /// compiler actually recorded is not: every extracted assembly is in this project's output, because the
    /// public-surface baselines already require it, so its reference list can simply be read.
    /// </remarks>
    [Fact]
    public void NoExtractedAssembly_ReferencesTheHost()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        var assemblyNames = DiscoverTransitionFolders(repositoryRoot)
            .Where(folder => folder.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(assemblyNames);

        var offenders = new List<string>();

        // Act
        foreach (var assemblyName in assemblyNames)
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");

            Assert.True(
                File.Exists(path),
                $"'{assemblyName}' is being extracted, so this rule must be able to read it, but it is not in " +
                "this test project's output. Add a ProjectReference to it.");

            var referenced = System.Reflection.Assembly.LoadFrom(path)
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null && name.Contains(_host, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();

            if (referenced.Count > 0)
            {
                offenders.Add($"{assemblyName} -> {string.Join(", ", referenced)}");
            }
        }

        // Assert
        Assert.True(
            offenders.Count == 0,
            "These extracted assemblies reference the host they are leaving: " +
            string.Join("; ", offenders));
    }

    /// <summary>
    /// Finds every <c>Transitions</c> folder in the repository.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The absolute paths of the folders, in a stable order.</returns>
    private static List<string> DiscoverTransitionFolders(string repositoryRoot)
    {
        return new[] { "src", "tests" }
            .Select(area => Path.Combine(repositoryRoot, area))
            .Where(Directory.Exists)
            .SelectMany(area => Directory.EnumerateDirectories(area, "Transitions", SearchOption.AllDirectories))
            .Where(folder =>
                !folder.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !folder.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Blanks out the parts of a file that may name the host without depending on it.
    /// </summary>
    /// <remarks>
    /// A build file is redacted differently from source. Every reference a project declares - a
    /// <c>PackageReference</c>, a <c>ProjectReference</c>, a <c>FrameworkReference</c> - names what it depends
    /// on inside a quoted attribute value, so blanking string literals in a project file blanks exactly the
    /// dependencies this rule exists to find. Only comments are removed from those.
    /// </remarks>
    /// <param name="file">The file being read, whose extension decides how it is redacted.</param>
    /// <param name="text">The file text.</param>
    /// <returns>The text with the parts that may name the host blanked out.</returns>
    private static string Redact(string file, string text)
    {
        var withoutComments = StripComments(text);

        return IsBuildFile(file) ? withoutComments : StripStringLiterals(withoutComments);
    }

    /// <summary>
    /// Determines whether a file declares references rather than code.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <returns><see langword="true"/> when the file is a project or properties file.</returns>
    private static bool IsBuildFile(string file)
        => !string.Equals(Path.GetExtension(file), ".cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Removes comments, so that mentioning the host is not read as depending on it.
    /// </summary>
    /// <remarks>
    /// Saying which host a seam exists for is the sort of thing these files should say.
    /// </remarks>
    /// <param name="text">The file text.</param>
    /// <returns>The text with its comments blanked out.</returns>
    private static string StripComments(string text)
    {
        return Regex.Replace(
            text,
            @"//.*?$|/\*.*?\*/|<!--.*?-->",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.Multiline);
    }

    /// <summary>
    /// Removes string literals from source, so that a value which merely contains the host name is not read as
    /// depending on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some of these literals cannot be renamed. Two are data-protection purposes - the key derivation reads
    /// them - so changing their text does not rename anything, it makes every user token and every stored
    /// recording encrypted under the old text permanently unreadable. Others are feature identifiers a host
    /// already has written into its database. These travel with the code precisely because they must not
    /// change.
    /// </para>
    /// <para>
    /// The cost is that a dependency expressed as a string, such as a type resolved by name at runtime, is not
    /// caught here. That is the narrower risk: it fails loudly the first time it runs, where a renamed
    /// derivation purpose fails silently and unrecoverably.
    /// </para>
    /// <para>
    /// Source only. A project file states every dependency it has inside a quoted attribute value, so blanking
    /// literals there would blank exactly what this rule is looking for.
    /// </para>
    /// </remarks>
    /// <param name="text">The source text, with its comments already removed.</param>
    /// <returns>The text with its string literals blanked out.</returns>
    private static string StripStringLiterals(string text)
    {
        return Regex.Replace(text, "\"(?:[^\"\\\\\\r\\n]|\\\\.)*\"", "\"\"");
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
