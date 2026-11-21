using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// An asynchronous contract that takes no <see cref="CancellationToken"/> stops cancellation at its boundary: the
/// endpoint passes <c>RequestAborted</c>, and every store call and HTTP call behind the contract keeps running after
/// the caller has gone (see CQ-3 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// The interface methods named <c>*Async</c> that take no token today are listed below with the number of overloads
/// each has. An entry may shrink or go, but no new method may join it. Add the token as the last parameter,
/// <c>CancellationToken cancellationToken = default</c>, and pass it on.
/// </para>
/// </summary>
public sealed partial class InterfaceCancellationTokenRatchetTests
{
    private static readonly Lazy<Scan> _scan = new(ScanSources);

    [Fact]
    public void NoNewInterfaceAsyncMethod_TakesNoCancellationToken()
    {
        Ratchet.AssertNothingAdded(
            _scan.Value.Offenders,
            _known,
            "These interface methods are asynchronous but take no CancellationToken, so cancellation stops at the contract.",
            "Add 'CancellationToken cancellationToken = default' as the last parameter and pass it to the stores and HTTP calls behind it.");
    }

    [Fact]
    public void EveryListedMethod_StillTakesNoToken()
    {
        Ratchet.AssertNothingStale(_scan.Value.Offenders, _known, $"{nameof(InterfaceCancellationTokenRatchetTests)}.{nameof(_known)}");
    }

    [Fact]
    public void Scan_ReadsTheInterfacesOfTheSourceTree()
    {
        Assert.True(_scan.Value.AsyncMethods > 1000, $"Only {_scan.Value.AsyncMethods} interface async methods were read; the scan no longer matches the source.");
    }

    [Theory]
    [InlineData("public interface IStore\n{\n    Task<Item> FindAsync(string id);\n}", "IStore.FindAsync")]
    [InlineData("public interface IStore\n{\n    ValueTask SaveAsync(Item item, CancellationToken cancellationToken = default);\n}", null)]
    [InlineData("public interface IStore<T> where T : class\n{\n    Task<IReadOnlyList<T>> ListAsync(\n        int page,\n        int size);\n}", "IStore.ListAsync")]
    [InlineData("public interface IStore\n{\n    Task RunAsync() => RunAsync(CancellationToken.None);\n    Task RunAsync(CancellationToken cancellationToken);\n}", "IStore.RunAsync")]
    [InlineData("public interface IStore\n{\n    // Task FindAsync(string id);\n    int Count { get; }\n}", null)]
    [InlineData("public interface IStore\n{\n    ValueTask<(bool Found, string Value)> TryGetAsync(string key);\n}", "IStore.TryGetAsync")]
    public void Read_FindsAsyncInterfaceMethodsWithoutAToken(string source, string expected)
    {
        // Act
        var offenders = Read(source).Where(method => !method.TakesToken).Select(method => method.Name).ToList();

        // Assert
        string[] expectedNames = expected is null ? [] : [expected];

        Assert.Equal(expectedNames, offenders);
    }

    private static Scan ScanSources()
    {
        var offenders = new Dictionary<string, int>(StringComparer.Ordinal);
        var asyncMethods = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            var source = Ratchet.ReadAllText(file);

            if (!source.Contains("interface", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var method in Read(source))
            {
                asyncMethods++;

                if (!method.TakesToken)
                {
                    var key = $"{file.RelativePath}: {method.Name}";
                    offenders[key] = offenders.GetValueOrDefault(key) + 1;
                }
            }
        }

        return new Scan(offenders, asyncMethods);
    }

    private static IEnumerable<InterfaceMethod> Read(string source)
    {
        var code = CSharpSourceMask.CommentsAndLiterals(source);

        foreach (Match declaration in InterfaceDeclaration().Matches(code))
        {
            var open = code.IndexOf('{', declaration.Index + declaration.Length);

            if (open < 0)
            {
                continue;
            }

            // A semicolon first means a declaration with no body, such as 'interface IMarker;'.
            var semicolon = code.IndexOf(';', declaration.Index + declaration.Length);

            if (semicolon >= 0 && semicolon < open)
            {
                continue;
            }

            var close = CSharpSourceMask.FindClosing(code, open);
            var body = code[(open + 1)..close];
            var name = declaration.Groups["name"].Value;

            foreach (Match method in AsyncMethodDeclaration().Matches(body))
            {
                // A call inside a default implementation is nested in a body of its own.
                if (Depth(body, method.Index) != 0)
                {
                    continue;
                }

                var parametersOpen = method.Index + method.Length - 1;
                var parametersClose = CSharpSourceMask.FindClosing(body, parametersOpen);
                var parameters = body[(parametersOpen + 1)..parametersClose];

                yield return new InterfaceMethod($"{name}.{method.Groups["name"].Value}", parameters.Contains("CancellationToken", StringComparison.Ordinal));
            }
        }
    }

    private static int Depth(string body, int index)
    {
        var depth = 0;

        for (var position = 0; position < index; position++)
        {
            if (body[position] == '{')
            {
                depth++;
            }
            else if (body[position] == '}')
            {
                depth--;
            }
        }

        return depth;
    }

    [GeneratedRegex(@"\binterface\s+(?<name>I[A-Z]\w*)")]
    private static partial Regex InterfaceDeclaration();

    // A return type, the name and the opening parenthesis. After '=>', 'return' or 'await' it is a call, not a declaration.
    [GeneratedRegex(@"(?<![\w.=>])(?!(?:return|await|new|throw|yield|else|case|in|is|as)\b)[\w.]+(?:<(?:[^<>()]|\([^()]*\)|<(?:[^<>()]|\([^()]*\)|<[^<>()]*>)*>)*>)?\??\s+(?<name>[A-Z]\w*Async)\s*(?:<[^<>()]*(?:<[^<>()]*>[^<>()]*)*>)?\s*\(")]
    private static partial Regex AsyncMethodDeclaration();

    private sealed record InterfaceMethod(string Name, bool TakesToken);

    private sealed record Scan(Dictionary<string, int> Offenders, int AsyncMethods);
}
