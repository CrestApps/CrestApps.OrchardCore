using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Tests.Core.Conditions;

/// <summary>
/// Runs the shared parity vectors in <c>tests/fixtures/conditions/condition-vectors.json</c>. The browser evaluator
/// (<c>tests/js/resources/conditions.test.js</c>) runs the same file, so the two cannot drift apart.
/// </summary>
public sealed class ConditionVectorTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<JsonObject> _vectors = new(Load);

    /// <summary>
    /// Gets the names of the rule vectors.
    /// </summary>
    public static TheoryData<string> RuleVectors => Names("rules");

    /// <summary>
    /// Gets the names of the hidden-item vectors.
    /// </summary>
    public static TheoryData<string> HiddenVectors => Names("hidden");

    [Theory]
    [MemberData(nameof(RuleVectors))]
    public void IsMet_RuleVector_MatchesTheExpectedResult(string name)
    {
        // Arrange
        var vector = Find("rules", name);
        var rule = vector["rule"].Deserialize<ConditionRule>(_options);
        var sources = vector["sources"]?.AsObject();

        // Act
        var result = ConditionEvaluator.IsMet(rule, source => Lookup(sources, source));

        // Assert
        Assert.Equal(vector["expected"]!.GetValue<bool>(), result);
    }

    [Theory]
    [MemberData(nameof(HiddenVectors))]
    public void GetHidden_HiddenVector_MatchesTheExpectedItems(string name)
    {
        // Arrange
        var vector = Find("hidden", name);
        var rules = vector["rules"].Deserialize<Dictionary<string, ConditionRule>>(_options);
        var sources = vector["sources"]?.AsObject();
        var expected = vector["hidden"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();

        // Act
        var hidden = ConditionalVisibility.GetHidden(rules, source => Lookup(sources, source));

        // Assert
        Assert.Equal(expected, hidden.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Vectors_CoverEveryOperator()
    {
        // Arrange
        var used = _vectors.Value["rules"]!.AsArray()
            .SelectMany(vector => Operators(vector["rule"]!.AsObject()))
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var missing = Enum.GetNames<ConditionOperator>().Where(name => !used.Contains(name)).ToArray();

        // Assert
        Assert.Empty(missing);
    }

    private static IEnumerable<string> Operators(JsonObject rule)
    {
        foreach (var condition in rule["Conditions"]?.AsArray() ?? [])
        {
            yield return condition!["Operator"]!.GetValue<string>();
        }

        foreach (var group in rule["Groups"]?.AsArray() ?? [])
        {
            foreach (var name in Operators(group!.AsObject()))
            {
                yield return name;
            }
        }
    }

    private static ConditionValue Lookup(JsonObject sources, string source)
    {
        if (sources is null || !sources.TryGetPropertyValue(source, out var node) || node is null)
        {
            return null;
        }

        if (node["withheld"]?.GetValue<bool>() == true)
        {
            return ConditionValue.Withheld;
        }

        var values = node["values"]?.AsArray().Select(item => item!.GetValue<string>()) ?? [];
        var ancestors = node["ancestors"]?.AsArray().Select(item => item!.GetValue<string>());

        return new ConditionValue(values, ancestors);
    }

    private static TheoryData<string> Names(string section)
    {
        var data = new TheoryData<string>();

        foreach (var vector in _vectors.Value[section]!.AsArray())
        {
            data.Add(vector!["name"]!.GetValue<string>());
        }

        return data;
    }

    private static JsonObject Find(string section, string name)
        => _vectors.Value[section]!.AsArray().Single(vector => vector!["name"]!.GetValue<string>() == name)!.AsObject();

    private static JsonObject Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tests", "fixtures", "conditions", "condition-vectors.json");

            if (File.Exists(path))
            {
                return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("tests/fixtures/conditions/condition-vectors.json was not found above the test output folder.");
    }
}
