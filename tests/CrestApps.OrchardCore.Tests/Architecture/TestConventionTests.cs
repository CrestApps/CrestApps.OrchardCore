using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Conventions of the test projects (see CQ-12 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// A test class is sealed: nothing derives from it, and an open class invites a base class whose tests run again in
/// every subclass. An abstract base class is fine.
/// </para>
/// <para>
/// <c>Task.Delay</c> in a test waits for real time: the test is slow when the delay is long and flaky when it is short.
/// Use <c>FakeTimeProvider</c>, or wait on the event the test expects. The files that delayed when the ratchet was set
/// are listed with their count, which may only shrink.
/// </para>
/// </summary>
public sealed partial class TestConventionTests
{
    private static readonly Lazy<(Dictionary<string, int> UnsealedClasses, Dictionary<string, int> Delays)> _actual = new(ScanTests);

    [Fact]
    public void EveryTestClass_IsSealed()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.UnsealedClasses,
            _knownUnsealedTestClasses,
            "These test classes are neither sealed nor abstract.",
            "Seal the class.");
    }

    [Fact]
    public void EveryListedTestClass_IsStillUnsealed()
    {
        Ratchet.AssertNothingStale(_actual.Value.UnsealedClasses, _knownUnsealedTestClasses, $"{nameof(TestConventionTests)}.{nameof(_knownUnsealedTestClasses)}");
    }

    [Fact]
    public void NoTest_AddsATaskDelay()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.Delays,
            _knownDelays,
            "These test files wait with Task.Delay, or wait more often than they did.",
            "Advance a FakeTimeProvider, or await the signal the test expects (a TaskCompletionSource, a channel read).");
    }

    [Fact]
    public void EveryListedDelay_StillExists()
    {
        Ratchet.AssertNothingStale(_actual.Value.Delays, _knownDelays, $"{nameof(TestConventionTests)}.{nameof(_knownDelays)}");
    }

    [Theory]
    [InlineData("public sealed class ATests\n{\n    [Fact]\n    public void Works() { }\n}", "")]
    [InlineData("public class ATests\n{\n    [Theory]\n    [InlineData(1)]\n    public void Works(int value) { }\n}", "ATests")]
    [InlineData("public abstract class TestsBase\n{\n    [Fact]\n    public void Works() { }\n}", "")]
    [InlineData("public sealed class ATests\n{\n    [Fact]\n    public void Works() { }\n\n    private class Helper\n    {\n        public void Run() { }\n    }\n}", "")]
    [InlineData("public sealed class ATests\n{\n    public class Nested\n    {\n        [Fact]\n        public void Works() { }\n    }\n}", "Nested")]
    public void ReadUnsealedTestClasses_FindsClassesWithTestsThatAreNotSealed(string source, string expected)
    {
        // Act
        var actual = string.Join(",", ReadUnsealedTestClasses(source));

        // Assert
        Assert.Equal(expected, actual);
    }

    private static (Dictionary<string, int>, Dictionary<string, int>) ScanTests()
    {
        var unsealed = new Dictionary<string, int>(StringComparer.Ordinal);
        var delays = new Dictionary<string, int>(StringComparer.Ordinal);
        var testFiles = 0;

        foreach (var file in Ratchet.EnumerateFiles("tests"))
        {
            var source = Ratchet.ReadAllText(file);

            if (source.Contains("[Fact", StringComparison.Ordinal) || source.Contains("[Theory", StringComparison.Ordinal))
            {
                testFiles++;

                foreach (var name in ReadUnsealedTestClasses(source))
                {
                    unsealed[$"{file.RelativePath}: {name}"] = 1;
                }
            }

            if (source.Contains("Task.Delay", StringComparison.Ordinal))
            {
                var count = TaskDelay().Count(CSharpSourceMask.Comments(source));

                if (count > 0)
                {
                    delays[file.RelativePath] = count;
                }
            }
        }

        Assert.True(testFiles > 1000, $"Only {testFiles} test files were scanned; the scan no longer matches the repository layout.");

        return (unsealed, delays);
    }

    private static IEnumerable<string> ReadUnsealedTestClasses(string source)
    {
        var code = CSharpSourceMask.CommentsAndLiterals(source);
        var classes = new List<(string Name, string Modifiers, int Open, int Close)>();

        foreach (Match declaration in ClassDeclaration().Matches(code))
        {
            var open = code.IndexOf('{', declaration.Index + declaration.Length);
            var semicolon = code.IndexOf(';', declaration.Index + declaration.Length);

            if (open < 0 || (semicolon >= 0 && semicolon < open))
            {
                continue;
            }

            classes.Add((declaration.Groups["name"].Value, declaration.Groups["modifiers"].Value, open, CSharpSourceMask.FindClosing(code, open)));
        }

        var testClasses = new HashSet<int>();

        foreach (Match attribute in TestAttribute().Matches(code))
        {
            // The innermost class around the attribute is the test class.
            var owner = -1;

            for (var index = 0; index < classes.Count; index++)
            {
                if (classes[index].Open < attribute.Index && attribute.Index < classes[index].Close &&
                    (owner < 0 || classes[index].Open > classes[owner].Open))
                {
                    owner = index;
                }
            }

            if (owner >= 0)
            {
                testClasses.Add(owner);
            }
        }

        foreach (var index in testClasses.Order())
        {
            var modifiers = classes[index].Modifiers;

            if (!Regex.IsMatch(modifiers, @"\b(?:sealed|abstract|static)\b"))
            {
                yield return classes[index].Name;
            }
        }
    }

    [GeneratedRegex(@"(?<modifiers>(?:\b(?:public|internal|private|protected|sealed|abstract|static|partial|file)\s+)*)\bclass\s+(?<name>\w+)")]
    private static partial Regex ClassDeclaration();

    [GeneratedRegex(@"\[\s*(?:Fact|Theory)\b")]
    private static partial Regex TestAttribute();

    [GeneratedRegex(@"\bTask\s*\.\s*Delay\s*\(")]
    private static partial Regex TaskDelay();
}
