using System.IO;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Stops new empty catch blocks (see <c>docs/engineering/reusability-audit-2026-11.md</c>, section 6). A catch that
/// swallows an exception says why in a comment, or logs it, so a reader can tell a deliberate skip from a lost
/// failure.
/// </summary>
public sealed partial class EmptyCatchRatchetTests
{
    [Fact]
    public void NoCatchBlock_IsEmptyWithoutAComment()
    {
        // Arrange
        var repositoryRoot = ProjectReferenceRulesTests.FindRepositoryRoot();
        var empty = new List<string>();
        var scanned = 0;

        // Act
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file))
            {
                continue;
            }

            scanned++;

            var text = File.ReadAllText(file);

            foreach (Match match in EmptyCatch().Matches(text))
            {
                var line = text.AsSpan(0, match.Index).Count('\n') + 1;

                empty.Add($"{Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')}:{line}");
            }
        }

        // Assert
        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");
        Assert.True(empty.Count == 0,
            "These catch blocks swallow an exception without saying why. Log it, or add a comment that explains why it is safe to ignore:"
            + Environment.NewLine + string.Join(Environment.NewLine, empty.Order(StringComparer.Ordinal).Select(entry => "    " + entry)));
    }

    [Theory]
    [InlineData("catch\n{\n}", 1)]
    [InlineData("catch (JsonException)\n        {\n        }", 1)]
    [InlineData("catch (RequestFailedException ex) when (ex.Status == 404)\n{\n}", 1)]
    [InlineData("catch (OperationCanceledException) when (token.IsCancellationRequested) { }", 1)]
    [InlineData("catch (JsonException)\n{\n    // The body is not JSON.\n}", 0)]
    [InlineData("catch (Exception ex)\n{\n    logger.LogError(ex, \"Failed.\");\n}", 0)]
    [InlineData("} catch (e) { }", 0)]
    public void EmptyCatch_MatchesEmptyBlocksButNotCommentedOnesOrScript(string code, int expected)
    {
        // Act
        var actual = EmptyCatch().Count(code);

        // Assert
        Assert.Equal(expected, actual);
    }

    // A C# catch names a PascalCase exception type, which keeps the catch (e) { } of a script inside a string out.
    [GeneratedRegex(@"\bcatch\b(?:\s*\(\s*[A-Z][\w.]*(?:\s+\w+)?\s*\))?(?:\s*when\s*\((?:[^()]|\([^()]*\))*\))?\s*\{\s*\}")]
    private static partial Regex EmptyCatch();

    private static bool IsExcluded(string path)
    {
        foreach (var folder in new[] { "bin", "obj", "node_modules" })
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
