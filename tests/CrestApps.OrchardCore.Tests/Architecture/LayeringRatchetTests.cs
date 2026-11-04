using System.IO;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Holds the project layers apart (see <c>AGENTS.md</c>, "Layering and project references"): a <c>.Core</c> library
/// never references a module, an <c>.Abstractions</c> library never references a <c>.Core</c> library or a module, and a
/// module references another module only inside its own family. The references that broke these rules when the
/// ratchet was set are listed in <see cref="_knownViolations"/>; that list may only shrink.
/// </summary>
public sealed class LayeringRatchetTests
{
    /// <summary>
    /// The module that every module may reference to use its tag helpers and view components in views.
    /// </summary>
    private const string ResourcesModule = "CrestApps.OrchardCore.Resources";

    /// <summary>
    /// Provider modules that extend a parent module whose name they do not start with. The key is the provider and the
    /// value the parent it may reference, as if its name started with the parent's.
    /// </summary>
    private static readonly Dictionary<string, string> _providerFamilies = new(StringComparer.Ordinal)
    {
        ["CrestApps.OrchardCore.OpenAI"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.OpenAI.Azure"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.AzureAIInference"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.Ollama"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.Cartesia"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.ElevenLabs"] = "CrestApps.OrchardCore.AI",
        ["CrestApps.OrchardCore.Asterisk"] = "CrestApps.OrchardCore.Telephony",
        ["CrestApps.OrchardCore.Telnyx"] = "CrestApps.OrchardCore.Telephony",
    };

    /// <summary>
    /// The references that broke a layering rule when the ratchet was set (November 2026 audit, section 4). Never add an
    /// entry: move the type the reference was for into the referenced building block's <c>.Abstractions</c> project
    /// instead. Remove an entry as soon as its reference is gone.
    /// </summary>
    private static readonly HashSet<string> _knownViolations = new(StringComparer.Ordinal)
    {
        // A .Core library that references a module (only for PhoneField; D19).
        "CrestApps.OrchardCore.Omnichannel.Core -> CrestApps.OrchardCore.ContentFields",

        // Modules that reference a module of another family, or a parent that references its own sub-module.
        "CrestApps.OrchardCore.Accounts -> CrestApps.OrchardCore.Addresses",
        "CrestApps.OrchardCore.AI -> CrestApps.OrchardCore.AI.Prompting",
        "CrestApps.OrchardCore.AI.Agent -> CrestApps.OrchardCore.Recipes",
        "CrestApps.OrchardCore.AI.Documents -> CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Documents -> CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.FileSources -> CrestApps.OrchardCore.AI.DataSources",
        "CrestApps.OrchardCore.AI.FileSources -> CrestApps.OrchardCore.AI.Documents",
        "CrestApps.OrchardCore.AI.Memory -> CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.WebCrawlers -> CrestApps.OrchardCore.AI.DataSources",
        "CrestApps.OrchardCore.AI.WebMcp -> CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.ContactCenter -> CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.ContactCenter -> CrestApps.OrchardCore.Omnichannel.Managements",
        "CrestApps.OrchardCore.ContentAccessControl -> CrestApps.OrchardCore.Roles",
        "CrestApps.OrchardCore.ContentFields -> CrestApps.OrchardCore.PhoneNumbers",
        "CrestApps.OrchardCore.Dispatch -> CrestApps.OrchardCore.Workforce",
        "CrestApps.OrchardCore.DncRegistry -> CrestApps.OrchardCore.PhoneNumbers",
        "CrestApps.OrchardCore.NLWeb -> CrestApps.OrchardCore.Seo",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.Accounts",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.ContentFields",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.DncRegistry",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.PhoneNumbers",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.TimeZones",
        "CrestApps.OrchardCore.Omnichannel.Managements -> CrestApps.OrchardCore.Users",
        "CrestApps.OrchardCore.Omnichannel.Voice -> CrestApps.OrchardCore.Omnichannel.Managements",
        "CrestApps.OrchardCore.Omnichannel.Voice -> CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.PointOfSale -> CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Telnyx -> CrestApps.OrchardCore.Omnichannel.Voice",
        "CrestApps.OrchardCore.Telnyx -> CrestApps.OrchardCore.WebSockets",
    };

    [Fact]
    public void NoProject_AddsAReferenceThatBreaksTheLayers()
    {
        // Arrange
        var violations = FindViolations();

        // Act
        var added = violations
            .Where(violation => !_knownViolations.Contains(violation.Reference))
            .Select(violation => $"{violation.Reference}: {violation.Rule}")
            .ToList();

        // Assert
        Assert.True(added.Count == 0, BuildMessage(
            "These project references break the layering rules in AGENTS.md (\"Layering and project references\"). " +
            "Move the type you need into the referenced building block's .Abstractions project and reference that instead:",
            added));
    }

    [Fact]
    public void EveryKnownViolation_StillOccurs()
    {
        // Arrange
        var actual = FindViolations()
            .Select(violation => violation.Reference)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var stale = _knownViolations
            .Where(reference => !actual.Contains(reference))
            .ToList();

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(
            $"These references are gone. Delete their entries from {nameof(LayeringRatchetTests)}._knownViolations so they cannot come back:",
            stale));
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.AI.Documents.AzureAI", "CrestApps.OrchardCore.AI.Documents", true)]
    [InlineData("CrestApps.OrchardCore.OpenAI", "CrestApps.OrchardCore.AI", true)]
    [InlineData("CrestApps.OrchardCore.Accounts.B2B", "CrestApps.OrchardCore.Accounts", true)]
    [InlineData("CrestApps.OrchardCore.Workforce", ResourcesModule, true)]
    [InlineData("CrestApps.OrchardCore.AIX", "CrestApps.OrchardCore.AI", false)]
    [InlineData("CrestApps.OrchardCore.AI", "CrestApps.OrchardCore.AI.Prompting", false)]
    [InlineData("CrestApps.OrchardCore.Dispatch", "CrestApps.OrchardCore.Workforce", false)]
    public void IsSameFamily_AllowsSubModulesProvidersAndResourcesOnly(string module, string referenced, bool expected)
    {
        // Act
        var actual = IsSameFamily(module, referenced);

        // Assert
        Assert.Equal(expected, actual);
    }

    private static List<(string Reference, string Rule)> FindViolations()
    {
        var sourceRoot = Path.Combine(ProjectReferenceRulesTests.FindRepositoryRoot(), "src");
        var layers = new Dictionary<string, string>(StringComparer.Ordinal);
        var projects = new List<(string Name, string Layer, string Path)>();

        foreach (var layer in new[] { "Abstractions", "Core", "Modules" })
        {
            foreach (var project in Directory.EnumerateFiles(Path.Combine(sourceRoot, layer), "*.csproj", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(project))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(project);

                layers[name] = layer;
                projects.Add((name, layer, project));
            }
        }

        Assert.True(projects.Count > 150, $"Only {projects.Count} projects were found under src; the scan no longer matches the repository layout.");

        var violations = new List<(string Reference, string Rule)>();

        foreach (var (name, layer, path) in projects)
        {
            foreach (var reference in ProjectReferenceRulesTests.ReadProjectReferenceNames(path))
            {
                if (!layers.TryGetValue(reference, out var referencedLayer))
                {
                    continue;
                }

                var rule = (layer, referencedLayer) switch
                {
                    ("Core", "Modules") => "a .Core library never references a module project",
                    ("Abstractions", "Core") => "an .Abstractions library never references a .Core library",
                    ("Abstractions", "Modules") => "an .Abstractions library never references a module project",
                    ("Modules", "Modules") when !IsSameFamily(name, reference) => "a module references only modules of its own family and Resources",
                    _ => null,
                };

                if (rule is not null)
                {
                    violations.Add(($"{name} -> {reference}", rule));
                }
            }
        }

        return violations;
    }

    private static bool IsSameFamily(string module, string referenced)
    {
        if (string.Equals(referenced, ResourcesModule, StringComparison.Ordinal) ||
            module.StartsWith(referenced + ".", StringComparison.Ordinal))
        {
            return true;
        }

        return _providerFamilies.TryGetValue(module, out var parent) &&
            (string.Equals(parent, referenced, StringComparison.Ordinal) || parent.StartsWith(referenced + ".", StringComparison.Ordinal));
    }

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string BuildMessage(string header, IEnumerable<string> lines)
    {
        var message = new StringBuilder().AppendLine(header);

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }
}
