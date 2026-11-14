using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A public type is found by its file name. When a file holds several, the second one is found by searching, and the
/// next type of its kind is added beside it instead of in a file of its own (see CQ-10 in
/// <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// The files with more than one public type (nested public types included) when the ratchet was set are listed with
/// their count, which may only shrink. Move each public type to a file named after it.
/// </para>
/// </summary>
public sealed partial class OneTypePerFileRatchetTests
{
    private static readonly Lazy<Dictionary<string, int>> _actual = new(ScanSources);

    [Fact]
    public void NoFile_DeclaresMoreThanOnePublicType()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value,
            _known,
            "These files declare more than one public type, or more than they did.",
            "Move each public type to a file named after it.");
    }

    [Fact]
    public void EveryListedFile_StillDeclaresThatManyPublicTypes()
    {
        Ratchet.AssertNothingStale(_actual.Value, _known, $"{nameof(OneTypePerFileRatchetTests)}.{nameof(_known)}");
    }

    [Theory]
    [InlineData("public sealed class A { }\npublic enum B { One }\npublic sealed record C(string Name);", 3)]
    [InlineData("public static partial class A\n{\n    public sealed class Nested { }\n}", 2)]
    [InlineData("public sealed class A { }\ninternal sealed class B { }\nfile sealed class C { }", 1)]
    [InlineData("public delegate Task Handler(string value);\npublic readonly record struct Point(int X, int Y);", 2)]
    [InlineData("// public class Commented { }\npublic interface IA { }\nvar text = \"public class InString { }\";", 1)]
    public void Count_CountsPublicTypeDeclarations(string source, int expected)
    {
        // Act
        var actual = PublicType().Count(CSharpSourceMask.CommentsAndLiterals(source));

        // Assert
        Assert.Equal(expected, actual);
    }

    private static Dictionary<string, int> ScanSources()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var scanned = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            scanned++;

            var count = PublicType().Count(CSharpSourceMask.CommentsAndLiterals(Ratchet.ReadAllText(file)));

            if (count > 1)
            {
                result[file.RelativePath] = count;
            }
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");

        return result;
    }

    [GeneratedRegex(@"\bpublic\s+(?:(?:static|sealed|abstract|partial|readonly|ref|unsafe|new)\s+)*(?:class|struct|interface|enum|delegate|record)\b")]
    private static partial Regex PublicType();
}
