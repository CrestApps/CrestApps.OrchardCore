using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A method longer than a screenful several times over does several things in an order nobody can see at once, and a
/// change to it is made by finding the nearest similar block and copying it (see CQ-10 in
/// <c>docs/engineering/review-2026-11/04-code-quality.md</c>: <c>SmsOmnichannelEventHandler.HandleAsync</c> was 856
/// lines).
/// <para>
/// The methods with a block body over <see cref="MaximumLines"/> lines when the ratchet was set are listed with their
/// length, which may only shrink. When a file has several long methods of one name, the longest is recorded.
/// </para>
/// </summary>
public sealed partial class MethodLengthRatchetTests
{
    private const int MaximumLines = 150;

    private static readonly Lazy<Dictionary<string, int>> _actual = new(ScanSources);

    [Fact]
    public void NoMethod_IsLongerThanTheLimit()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value,
            _known,
            $"These methods are over {MaximumLines} lines, or longer than they were.",
            "Split the method into steps with names, or move a step to a collaborator.");
    }

    [Fact]
    public void EveryListedMethod_IsStillOverTheLimit()
    {
        Ratchet.AssertNothingStale(_actual.Value, _known, $"{nameof(MethodLengthRatchetTests)}.{nameof(_known)}");
    }

    [Theory]
    [InlineData("    public async Task<IActionResult> Index(string id, CancellationToken token)\n    {\n        var x = \"}\";\n        return View();\n    }", "Index", 5)]
    [InlineData("    private static IEnumerable<T> Read<T>(string source)\n        where T : class\n    {\n        yield break;\n    }", "Read", 5)]
    [InlineData("    public override string ToString() => Name;", null, 0)]
    [InlineData("    public Service(IA a)\n    {\n        _a = a;\n    }", null, 0)]
    [InlineData("    public string Name { get; set; }", null, 0)]
    [InlineData("    private async Task<(bool Ok, string Error)> BeginAsync(int id)\n    {\n        return (true, null);\n    }", "BeginAsync", 4)]
    [InlineData("    private static (int First, List<int> Rest) Split(int[] values)\n    {\n        return default;\n    }", "Split", 4)]
    public void Read_MeasuresBlockBodiedMethods(string source, string name, int lines)
    {
        // Act
        var methods = Read(source).ToList();

        // Assert
        if (name is null)
        {
            Assert.Empty(methods);
        }
        else
        {
            var method = Assert.Single(methods);

            Assert.Equal(name, method.Name);
            Assert.Equal(lines, method.Lines);
        }
    }

    private static Dictionary<string, int> ScanSources()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var methods = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            foreach (var method in Read(Ratchet.ReadAllText(file)))
            {
                methods++;

                if (method.Lines > MaximumLines)
                {
                    var key = $"{file.RelativePath}: {method.Name}";
                    result[key] = Math.Max(result.GetValueOrDefault(key), method.Lines);
                }
            }
        }

        Assert.True(methods > 10000, $"Only {methods} methods were read; the scan no longer matches the source.");

        return result;
    }

    private static IEnumerable<Method> Read(string source)
    {
        var code = CSharpSourceMask.CommentsAndLiterals(source);

        foreach (Match declaration in MethodDeclaration().Matches(code))
        {
            var parametersOpen = declaration.Index + declaration.Length - 1;
            var parametersClose = CSharpSourceMask.FindClosing(code, parametersOpen);

            // After the parameters come optional constraints, then the body: a block, an arrow or a semicolon.
            var position = parametersClose + 1;

            while (position < code.Length && code[position] is not '{' and not ';' and not '=')
            {
                position++;
            }

            if (position >= code.Length || code[position] != '{')
            {
                continue;
            }

            var close = CSharpSourceMask.FindClosing(code, position);

            yield return new Method(
                declaration.Groups["name"].Value,
                Ratchet.LineAt(code, close) - Ratchet.LineAt(code, declaration.Index) + 1);
        }
    }

    // A member declaration at the start of a line: modifiers, a return type (a tuple too, alone or as a type argument),
    // the name, type parameters and '('.
    [GeneratedRegex(@"^[ \t]*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|extern|new|unsafe|partial)\s+)+(?!(?:class|struct|record|interface|enum|delegate|event|operator|implicit|explicit)\b)(?:[\w.]+(?:<(?:[^<>()]|\([^()]*\)|<(?:[^<>()]|\([^()]*\)|<[^<>()]*>)*>)*>)?|\((?:[^()]|\([^()]*\))*\))(?:\[\])?\??[ \t]+(?<name>\w+)\s*(?:<[^<>()]*(?:<[^<>()]*>[^<>()]*)*>)?\s*\(", RegexOptions.Multiline)]
    private static partial Regex MethodDeclaration();

    private sealed record Method(string Name, int Lines);
}
