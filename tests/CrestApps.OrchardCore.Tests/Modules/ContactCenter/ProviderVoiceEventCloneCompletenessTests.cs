using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins that copying a provider voice event copies all of it. The copy is taken before the event is projected
/// onto a call session, so a field the copy forgets is not merely absent — it is silently replaced by whatever
/// the projection infers in its place. <c>HangupCause</c> was dropped this way, and because the session falls
/// back to inferring a cause from the call state, every call reported the inferred cause instead of the one the
/// provider actually gave, with nothing anywhere to say the real one had been lost.
/// </summary>
public sealed class ProviderVoiceEventCloneCompletenessTests
{
    private const string ServiceFileName = "ProviderVoiceEventService.cs";
    private const string CloneMethodName = "CloneProviderEvent";

    [Fact]
    public void CloningAProviderVoiceEvent_CopiesEverySettableProperty()
    {
        // Arrange
        var expected = typeof(ProviderVoiceEvent)
            .GetProperties()
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .ToArray();

        var clone = FindCloneMethod();

        // Act
        var copied = clone
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Parent is InitializerExpressionSyntax)
            .Select(assignment => (assignment.Left as IdentifierNameSyntax)?.Identifier.ValueText)
            .Where(name => name is not null)
            .ToArray();

        // Assert
        var missing = expected.Except(copied, StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0,
            $"{CloneMethodName} does not copy: {string.Join(", ", missing)}.");
    }

    private static MethodDeclarationSyntax FindCloneMethod()
    {
        var file = Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ServiceFileName, SearchOption.AllDirectories)
            .Single(candidate => !candidate.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        return CSharpSyntaxTree.ParseText(File.ReadAllText(file))
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => string.Equals(method.Identifier.ValueText, CloneMethodName, StringComparison.Ordinal));
    }

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
