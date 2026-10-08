using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the single call-state vocabulary. The soft-phone <c>CallState</c> vocabulary has seven
/// states and the Contact Center vocabulary has twelve, so translating between them is lossy in one
/// direction and ambiguous in the other. While every call site was free to write its own switch,
/// they disagreed: one widened an unknown state to <c>Dialing</c> and another to <c>Ended</c>, and
/// every one of them discarded the hangup outcome, so a busy number, an unanswered dial, and a
/// completed conversation all became <c>Ended</c>.
/// </summary>
public sealed partial class ContactCenterCallStateProjectionArchitectureTests
{
    private const string ProjectionFileName = "VoiceCallStateProjection.cs";

    [Fact]
    public void NoProductionSourceOutsideTheProjection_TranslatesBetweenTheTwoCallStateVocabularies()
    {
        // Arrange
        var sourceFiles = EnumerateProductionSources();

        // Act
        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var source = File.ReadAllText(file);

            if (WideningProjection().IsMatch(source))
            {
                violations.Add($"{file} widens a telephony CallState into a VoiceCallState.");
            }

            if (NarrowingProjection().IsMatch(source))
            {
                violations.Add($"{file} narrows a VoiceCallState into a telephony CallState.");
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void TheProjection_IsPresentAndIsTheOnlyDeclaredTranslation()
    {
        // Arrange
        var projectionFiles = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ProjectionFileName, SearchOption.AllDirectories)
            .ToList();

        // Act
        var source = Assert.Single(projectionFiles);

        // Assert
        Assert.Matches(WideningProjection(), File.ReadAllText(source));
    }

    private static IEnumerable<string> EnumerateProductionSources()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(file) ?? string.Empty;

            if (directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                string.Equals(Path.GetFileName(file), ProjectionFileName, StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }

    // A switch arm that reads a telephony CallState and yields a VoiceCallState. The negative
    // lookbehind is required because "VoiceCallState." contains "CallState." as a substring.
    [GeneratedRegex(@"(?<!ContactCenter)\bCallState\.\w+[^\r\n]*=>\s*VoiceCallState\.")]
    private static partial Regex WideningProjection();

    [GeneratedRegex(@"\bContactCenterCallState\.\w+[^\r\n]*=>\s*(?<!ContactCenter)CallState\.")]
    private static partial Regex NarrowingProjection();

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
