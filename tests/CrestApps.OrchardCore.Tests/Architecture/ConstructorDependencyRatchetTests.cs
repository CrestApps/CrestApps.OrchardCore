using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A constructor with more than a dozen dependencies belongs to a type that does several jobs: it is hard to test, every
/// change touches it, and a missing registration in one tenant breaks all of its jobs at once (see CQ-10 in
/// <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// The constructors (including primary constructors of classes and structs) over <see cref="MaximumParameters"/>
/// parameters when the ratchet was set are listed with their count, which may only shrink. Positional records are not
/// counted: their parameters are the record's data, not its dependencies.
/// </para>
/// </summary>
public sealed partial class ConstructorDependencyRatchetTests
{
    private const int MaximumParameters = 12;

    private static readonly Lazy<Dictionary<string, int>> _actual = new(ScanSources);

    [Fact]
    public void NoConstructor_TakesMoreDependenciesThanTheLimit()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value,
            _known,
            $"These constructors take more than {MaximumParameters} parameters, or more than they did.",
            "Split the type by job, or move a group of collaborators behind a service of their own.");
    }

    [Fact]
    public void EveryListedConstructor_IsStillOverTheLimit()
    {
        Ratchet.AssertNothingStale(_actual.Value, _known, $"{nameof(ConstructorDependencyRatchetTests)}.{nameof(_known)}");
    }

    [Theory]
    [InlineData("public sealed class Service\n{\n    public Service(IA a, IB b, IEnumerable<IC> c, IOptions<D> d)\n    {\n    }\n}", "Service", 4)]
    [InlineData("internal sealed class Service(IA a, IB b, IStringLocalizer<Service> S) : IService\n{\n}", "Service", 3)]
    [InlineData("public sealed record ApiCoupon(string Id, string Code, decimal Amount);", null, 0)]
    [InlineData("public sealed class Service\n{\n    public Service()\n    {\n    }\n\n    private void Run(IA a, IB b) { }\n}", "Service", 0)]
    public void Read_CountsConstructorParameters(string source, string type, int expected)
    {
        // Act
        var constructors = Read(source).ToList();

        // Assert
        if (type is null)
        {
            Assert.Empty(constructors);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(constructors, constructor => constructor.Type == type).Parameters);
        }
    }

    private static Dictionary<string, int> ScanSources()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var constructors = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            foreach (var constructor in Read(Ratchet.ReadAllText(file)))
            {
                constructors++;

                if (constructor.Parameters > MaximumParameters)
                {
                    var key = $"{file.RelativePath}: {constructor.Type}";
                    result[key] = Math.Max(result.GetValueOrDefault(key), constructor.Parameters);
                }
            }
        }

        Assert.True(constructors > 3000, $"Only {constructors} constructors were read; the scan no longer matches the source.");

        return result;
    }

    private static IEnumerable<Constructor> Read(string source)
    {
        var code = CSharpSourceMask.CommentsAndLiterals(source);

        foreach (Match declaration in TypeDeclaration().Matches(code))
        {
            var type = declaration.Groups["name"].Value;

            // A primary constructor follows the name and its type parameters.
            var afterName = declaration.Index + declaration.Length;

            while (afterName < code.Length && char.IsWhiteSpace(code[afterName]))
            {
                afterName++;
            }

            if (afterName < code.Length && code[afterName] == '<')
            {
                afterName = CSharpSourceMask.FindClosing(code, afterName) + 1;

                while (afterName < code.Length && char.IsWhiteSpace(code[afterName]))
                {
                    afterName++;
                }
            }

            if (declaration.Groups["kind"].Value != "record" && afterName < code.Length && code[afterName] == '(')
            {
                yield return new Constructor(type, ParameterCount(code, afterName));
            }

            foreach (Match constructor in Regex.Matches(code, $@"(?:\b(?:public|internal|protected|private)\s+){Regex.Escape(type)}\s*\("))
            {
                yield return new Constructor(type, ParameterCount(code, constructor.Index + constructor.Length - 1));
            }
        }
    }

    private static int ParameterCount(string code, int open)
        => CSharpSourceMask.CountParameters(code[(open + 1)..CSharpSourceMask.FindClosing(code, open)]);

    [GeneratedRegex(@"\b(?<kind>class|struct|record)\s+(?<name>[A-Z]\w*)")]
    private static partial Regex TypeDeclaration();

    private sealed record Constructor(string Type, int Parameters);
}
