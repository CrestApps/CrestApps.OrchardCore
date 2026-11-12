using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A module that reads a configuration section describes it in a <c>ConfigurationSchema.json</c> beside its project, so
/// an operator's editor completes and checks <c>appsettings.json</c> and the settings are documented where they are
/// read (see CQ-9 in <c>docs/engineering/review-2026-11/04-code-quality.md</c> and section 4 of
/// <c>docs/engineering/building-blocks.md</c>).
/// <para>
/// This reads every <c>GetSection(...)</c> on a configuration and every <c>BindConfiguration(...)</c> in a module,
/// resolves the section path (string literals and string constants), and looks for it under <c>OrchardCore</c> in the
/// module's schema. The sections that had no schema entry when the ratchet was set are listed; the list only shrinks.
/// </para>
/// </summary>
public sealed partial class ConfigurationSchemaCoverageTests
{
    private const string SchemaFileName = "ConfigurationSchema.json";

    private static readonly Lazy<Dictionary<string, int>> _actual = new(ScanModules);

    [Fact]
    public void EveryBoundSection_HasAnEntryInTheModuleSchema()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value,
            _knownUndescribedSections,
            "These modules bind a configuration section that their ConfigurationSchema.json does not describe.",
            $"Add the section, with a type, a default and a description per setting, to {SchemaFileName} in the module's folder (see the Dispatch module's).");
    }

    [Fact]
    public void EveryListedSection_IsStillUndescribed()
    {
        Ratchet.AssertNothingStale(_actual.Value, _knownUndescribedSections, $"{nameof(ConfigurationSchemaCoverageTests)}.{nameof(_knownUndescribedSections)}");
    }

    [Fact]
    public void EverySchema_IsValidJsonUnderOrchardCore()
    {
        // Arrange
        var schemas = Directory.EnumerateFiles(Path.Combine(Ratchet.RepositoryRoot, "src", "Modules"), SchemaFileName, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        // Assert
        Assert.NotEmpty(schemas);

        foreach (var schema in schemas)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schema));

            Assert.True(
                document.RootElement.TryGetProperty("properties", out var properties) && properties.TryGetProperty("OrchardCore", out _),
                $"{Path.GetRelativePath(Ratchet.RepositoryRoot, schema)} does not describe its settings under properties.OrchardCore.");
        }
    }

    [Theory]
    [InlineData("services.Configure<A>(_shellConfiguration.GetSection(\"CrestApps:DataConnectors\"));", "CrestApps:DataConnectors")]
    [InlineData("services.AddOptions<B>().BindConfiguration(\"CrestApps:Retention\");", "CrestApps:Retention")]
    [InlineData("_shellConfiguration.GetSection(Names.Section).Bind(options);", "CrestApps:Sample")]
    [InlineData("services.Configure<C>(_shellConfiguration.GetSection($\"{Names.Section}:Chat\"));", "CrestApps:Sample:Chat")]
    [InlineData("if (section.GetSection(nameof(Options.Urls)).Exists()) { }", null)]
    public void ReadSections_ResolvesLiteralsAndConstants(string code, string expected)
    {
        // Arrange
        var constants = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Names.Section"] = "CrestApps:Sample",
            ["Section"] = "CrestApps:Sample",
        };

        // Act
        var sections = ReadSections(code, constants, constants).ToList();

        // Assert
        string[] expectedSections = expected is null ? [] : [expected];

        Assert.Equal(expectedSections, sections);
    }

    private static Dictionary<string, int> ScanModules()
    {
        var constants = ReadConstants(Ratchet.EnumerateFiles("src"));
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var modulesRoot = Path.Combine(Ratchet.RepositoryRoot, "src", "Modules");
        var bindingModules = 0;

        foreach (var moduleDirectory in Directory.EnumerateDirectories(modulesRoot))
        {
            var module = Path.GetFileName(moduleDirectory);
            var files = Ratchet.EnumerateFiles(Path.Combine("src", "Modules", module)).ToList();
            var localConstants = ReadConstants(files);
            var sections = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var source = Ratchet.ReadAllText(file);

                if (source.Contains("GetSection", StringComparison.Ordinal) || source.Contains("BindConfiguration", StringComparison.Ordinal))
                {
                    sections.UnionWith(ReadSections(CSharpSourceMask.Comments(source), localConstants, constants));
                }
            }

            if (sections.Count == 0)
            {
                continue;
            }

            bindingModules++;

            var schemaPath = Path.Combine(moduleDirectory, SchemaFileName);
            using var schema = File.Exists(schemaPath) ? JsonDocument.Parse(File.ReadAllText(schemaPath)) : null;

            foreach (var section in sections)
            {
                if (schema is null || !Describes(schema.RootElement, section))
                {
                    result[$"{module}: {section}"] = 1;
                }
            }
        }

        Assert.True(bindingModules > 10, $"Only {bindingModules} modules were found to bind configuration; the scan no longer matches the source.");

        return result;
    }

    /// <summary>
    /// Whether the schema has the section's path under <c>properties.OrchardCore</c>, or a parent of it that takes any key.
    /// </summary>
    private static bool Describes(JsonElement schema, string section)
    {
        if (!schema.TryGetProperty("properties", out var root) || !root.TryGetProperty("OrchardCore", out var node))
        {
            return false;
        }

        foreach (var segment in section.Split(':'))
        {
            if (node.TryGetProperty("properties", out var properties) && properties.TryGetProperty(segment, out var child))
            {
                node = child;
            }
            else if (node.TryGetProperty("additionalProperties", out var any) && any.ValueKind == JsonValueKind.Object)
            {
                node = any;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> ReadSections(string code, IReadOnlyDictionary<string, string> localConstants, IReadOnlyDictionary<string, string> constants)
    {
        foreach (Match match in SectionCall().Matches(code))
        {
            var open = match.Index + match.Length - 1;
            var close = CSharpSourceMask.FindClosing(code, open);

            yield return Resolve(code[(open + 1)..close].Trim(), localConstants, constants);
        }
    }

    /// <summary>
    /// Evaluates a section argument made of string literals, interpolations and string constants joined by '+'.
    /// </summary>
    private static string Resolve(string expression, IReadOnlyDictionary<string, string> localConstants, IReadOnlyDictionary<string, string> constants)
    {
        if (expression.StartsWith("$\"", StringComparison.Ordinal) && expression.EndsWith('"'))
        {
            return Interpolation().Replace(expression[2..^1], hole => Resolve(hole.Groups["name"].Value, localConstants, constants));
        }

        if (expression.Contains('+', StringComparison.Ordinal))
        {
            return string.Concat(expression.Split('+').Select(part => Resolve(part.Trim(), localConstants, constants)));
        }

        if (expression.Length >= 2 && expression[0] == '"' && expression[^1] == '"')
        {
            return expression[1..^1];
        }

        var name = expression.Trim();

        if (name.StartsWith("nameof(", StringComparison.Ordinal))
        {
            return name[7..^1].Split('.')[^1];
        }

        if (localConstants.TryGetValue(name, out var value) || constants.TryGetValue(name, out value))
        {
            return value;
        }

        var parts = name.Split('.');
        var shortName = parts[^1];

        // A namespace-qualified constant is recorded by its type and name.
        if (parts.Length > 2 && constants.TryGetValue($"{parts[^2]}.{shortName}", out value))
        {
            return value;
        }

        // A constant of a nested type is recorded under the innermost type, so fall back to its name alone, which is
        // recorded only when every constant of that name has the same value.
        return localConstants.TryGetValue(shortName, out value) || constants.TryGetValue(shortName, out value)
            ? value
            : $"(unresolved {name})";
    }

    /// <summary>
    /// Reads the string constants of a set of files, by name and by the name of the type that declares them.
    /// </summary>
    private static Dictionary<string, string> ReadConstants(IEnumerable<Ratchet.SourceFile> files)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var source = Ratchet.ReadAllText(file);

            if (!source.Contains("const string", StringComparison.Ordinal))
            {
                continue;
            }

            var code = CSharpSourceMask.Comments(source);
            var types = TypeDeclaration().Matches(code);

            foreach (Match constant in StringConstant().Matches(code))
            {
                var type = types.LastOrDefault(declaration => declaration.Index < constant.Index)?.Groups["name"].Value;
                var name = constant.Groups["name"].Value;
                var value = constant.Groups["value"].Value;

                if (type is not null)
                {
                    constants[$"{type}.{name}"] = value;
                }

                if (constants.TryGetValue(name, out var existing) && existing != value)
                {
                    ambiguous.Add(name);
                }

                constants[name] = value;
            }
        }

        foreach (var name in ambiguous)
        {
            constants.Remove(name);
        }

        return constants;
    }

    [GeneratedRegex(@"(?:\b_?\w*[cC]onfiguration\b|IConfiguration>\(\))\s*\.\s*GetSection\s*\(|\bBindConfiguration\s*\(")]
    private static partial Regex SectionCall();

    [GeneratedRegex(@"\{(?<name>[\w.]+)\}")]
    private static partial Regex Interpolation();

    [GeneratedRegex(@"\bconst\s+string\s+(?<name>\w+)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex StringConstant();

    [GeneratedRegex(@"\b(?:class|struct|record|interface)\s+(?<name>\w+)")]
    private static partial Regex TypeDeclaration();
}
