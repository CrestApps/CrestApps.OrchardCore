using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards the provider-neutral boundary. Asterisk names its call legs "channels", groups them into
/// "bridges", and taps one with a "snoop" channel; none of those words describe an obligation another
/// provider can honor. While that vocabulary was declared in the shared Contact Center contracts, every
/// future provider inherited metadata keys it could never populate, and the shared contract silently
/// documented one vendor's implementation as if it were the platform's.
/// </summary>
public sealed partial class ProviderNeutralContractArchitectureTests
{
    private static readonly string[] _providerNeutralProjects =
    [
        Path.Combine("src", "Abstractions", "CrestApps.OrchardCore.Telephony.Abstractions"),
        Path.Combine("src", "Abstractions", "CrestApps.OrchardCore.ContactCenter.Abstractions"),
        Path.Combine("src", "Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Telephony"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.ContactCenter"),
    ];

    private static readonly string[] _providerPrivateMetadataKeys =
    [
        "snoopChannelId",
        "supervisorBridgeId",
        "transferBridgeId",
        "conferenceBridgeId",
        "attendedTransferBridgeId",
        "supervisorChannelId",
        "transferNewChannelId",
        "conferenceParticipantChannelId",
        "attendedTransferConsultChannelId",
    ];

    [Fact]
    public void ProviderNeutralProjects_DeclareNoProviderSpecificVocabulary()
    {
        // Arrange
        var scanned = 0;

        // Act
        var violations = new List<string>();

        foreach (var file in EnumerateProviderNeutralSources())
        {
            scanned++;
            var source = StripComments(File.ReadAllText(file));

            foreach (Match match in ProviderVocabulary().Matches(source))
            {
                violations.Add($"{RelativePath(file)} uses the provider-specific term '{match.Value}'.");
            }
        }

        // Assert
        Assert.Empty(violations);
        Assert.True(scanned > 200, $"The scan only inspected {scanned} files, which is too few to be meaningful.");
    }

    [Fact]
    public void ProviderPrivateMetadataKeys_AreReferencedOnlyInsideTheirOwningProviderModule()
    {
        // Arrange
        var owningModule = Path.Combine("src", "Modules", "CrestApps.OrchardCore.Asterisk") + Path.DirectorySeparatorChar;

        // Act
        var violations = new List<string>();

        foreach (var file in EnumerateProductionSources())
        {
            var relative = RelativePath(file);

            if (relative.StartsWith(owningModule, StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(file);

            foreach (var key in _providerPrivateMetadataKeys)
            {
                if (source.Contains($"\"{key}\"", StringComparison.Ordinal))
                {
                    violations.Add($"{relative} declares the provider-private metadata key '{key}'.");
                }
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ProviderPrivateMetadataKeys_AreActuallyUsedByTheOwningProviderModule()
    {
        // Arrange: without this floor the previous test would pass vacuously if the keys were simply deleted
        // rather than relocated, and the relocation would stop being proven.
        var owningModule = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Asterisk");
        var sources = Directory
            .EnumerateFiles(owningModule, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .Select(File.ReadAllText)
            .ToList();

        // Act
        var unused = _providerPrivateMetadataKeys
            .Where(key => !sources.Exists(source => source.Contains($"\"{key}\"", StringComparison.Ordinal)))
            .ToArray();

        // Assert
        Assert.Empty(unused);
    }

    [Fact]
    public void TheForbiddenVocabulary_MatchesRealProviderSource()
    {
        // Arrange: the forbidden terms must be words a provider module genuinely uses, otherwise the boundary
        // test would be guarding against vocabulary nobody was ever going to write.
        var asteriskRoot = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Asterisk");
        var providerSource = string.Join(
            '\n',
            Directory
                .EnumerateFiles(asteriskRoot, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsGenerated(file))
                .Select(file => StripComments(File.ReadAllText(file))));

        // Act
        var matches = ProviderVocabulary()
            .Matches(providerSource)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.True(matches.Length >= 4, $"The forbidden vocabulary matched only {matches.Length} distinct terms in the provider module.");
    }

    private static IEnumerable<string> EnumerateProviderNeutralSources()
    {
        var root = FindRepositoryRoot();

        foreach (var project in _providerNeutralProjects)
        {
            var projectRoot = Path.Combine(root, project);

            if (!Directory.Exists(projectRoot))
            {
                throw new InvalidOperationException($"The provider-neutral project '{project}' was not found.");
            }

            foreach (var file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (!IsGenerated(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProductionSources()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsGenerated(file))
            {
                yield return file;
            }
        }
    }

    private static bool IsGenerated(string file)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;

        return directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string StripComments(string source)
    {
        // The boundary being guarded is the declared contract: type names, member names, and metadata key
        // literals. Prose that names a provider as an example is documentation, not vocabulary another
        // provider inherits, so comments are excluded rather than forcing the docs to become vague.
        return CommentLine().Replace(BlockComment().Replace(source, string.Empty), string.Empty);
    }

    private static string RelativePath(string file)
        => Path.GetRelativePath(FindRepositoryRoot(), file);

    // Vendor product names and the call-topology vocabulary specific to a single provider's implementation.
    // "Channel" is deliberately absent: it is generic telephony vocabulary that several providers share.
    [GeneratedRegex(@"\b(?:[Aa]sterisk|ARI|PJSIP|Stasis|[Dd]ial[Pp]ad|[Ss]noop\w*|[Bb]ridgeId|[Bb]ridgeType|channelvars)\b")]
    private static partial Regex ProviderVocabulary();

    [GeneratedRegex(@"//[^\r\n]*")]
    private static partial Regex CommentLine();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

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
