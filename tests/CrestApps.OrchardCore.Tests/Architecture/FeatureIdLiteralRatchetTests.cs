using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Keeps feature ids out of string literals in manifests and in <c>[Feature]</c> and <c>[RequireFeatures]</c>
/// attributes (see <c>docs/engineering/reusability-audit-2026-11.md</c>, section 6). A CrestApps feature id is the
/// constant of the project that owns the feature; an Orchard Core one is a constant of <c>OrchardCoreFeatureIds</c>.
/// The files that still name a feature with a literal, because they cannot reach its constant without referencing
/// another module, are listed with their counts; a listed file may lose literals but never gain one.
/// </summary>
public sealed partial class FeatureIdLiteralRatchetTests
{
    /// <summary>
    /// The literals left when the ratchet was set. Never add an entry or raise a count. Lower a count or delete an
    /// entry as soon as a literal is replaced.
    /// </summary>
    private static readonly Dictionary<string, int> _knownLiterals = new(StringComparer.OrdinalIgnoreCase)
    {
        // The AI feature ids live in AI.Core, which the optional AI integrations must not reference; the Recipes module
        // still registers the schemas of modules it does not reference; a few modules have no constant for their own id.
        ["src/Modules/CrestApps.OrchardCore.AI.A2A/Startup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.DataSources/Startup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Mcp.Resources.Ftp/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Mcp.Resources.Sftp/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI.Mcp/Startup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.AI/Startup.cs"] = 4,
        ["src/Modules/CrestApps.OrchardCore.AzureAIInference/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Calendars/AIToolsStartup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.ContentAccessControl/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.ContentFields/Manifest.cs"] = 2,
        ["src/Modules/CrestApps.OrchardCore.ContentFields/Startup.cs"] = 2,
        ["src/Modules/CrestApps.OrchardCore.Dispatch/AIToolsStartup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.EventGrid/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.OpenAI.Azure/Manifest.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Recipes/ContactCenterRecipeSchemaStartups.cs"] = 17,
        ["src/Modules/CrestApps.OrchardCore.Recipes/Startup.cs"] = 13,
        ["src/Modules/CrestApps.OrchardCore.Scheduling/AIToolsStartup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Seo/Startup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Startup.cs"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Workforce/AIToolsStartup.cs"] = 3,
    };

    [Fact]
    public void NoFile_AddsAFeatureIdLiteral()
    {
        // Arrange
        var actual = ScanSources();
        var added = new List<string>();

        // Act
        foreach (var (file, count) in actual)
        {
            var allowed = _knownLiterals.GetValueOrDefault(file);

            if (count > allowed)
            {
                added.Add($"{file}: {count} feature id literals (allowed {allowed}).");
            }
        }

        // Assert
        Assert.True(added.Count == 0, BuildMessage(
            "These files name a feature with a string literal. Use the constant of the project that owns the feature, or OrchardCoreFeatureIds for an Orchard Core feature:",
            added));
    }

    [Fact]
    public void EveryKnownLiteral_StillExists()
    {
        // Arrange
        var actual = ScanSources();
        var stale = new List<string>();

        // Act
        foreach (var (file, allowed) in _knownLiterals)
        {
            var count = actual.GetValueOrDefault(file);

            if (count < allowed)
            {
                stale.Add(count == 0
                    ? $"{file} has no feature id literal any more. Delete its entry."
                    : $"{file} has {count} feature id literals, not {allowed}. Lower its count to {count}.");
            }
        }

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(
            $"Feature id literals were replaced. Update {nameof(FeatureIdLiteralRatchetTests)}._knownLiterals so they cannot come back:",
            stale));
    }

    [Theory]
    [InlineData("[RequireFeatures(\"CrestApps.OrchardCore.AI\")]", false, 1)]
    [InlineData("[RequireFeatures(\"OrchardCore.Workflows\", \"CrestApps.OrchardCore.AI\")]", false, 2)]
    [InlineData("[Feature(AIConstants.Feature.Area)]", false, 0)]
    [InlineData("[RequireFeatures(OrchardCoreFeatureIds.Workflows, CrestAppsRecipesConstants.Feature.Area)]", false, 0)]
    [InlineData("var area = \"CrestApps.OrchardCore.AI\";", false, 0)]
    [InlineData("        \"OrchardCore.Liquid\",", true, 1)]
    [InlineData("    Id = \"CrestApps.OrchardCore.AI.Chat.Copilot\",", true, 1)]
    [InlineData("    Name = \"AI Copilot Orchestrator\",", true, 0)]
    public void Count_FindsLiteralsInAttributesAndManifests(string line, bool isManifest, int expected)
    {
        // Act
        var actual = Count([line], isManifest);

        // Assert
        Assert.Equal(expected, actual);
    }

    private static Dictionary<string, int> ScanSources()
    {
        var repositoryRoot = ProjectReferenceRulesTests.FindRepositoryRoot();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file))
            {
                continue;
            }

            scanned++;

            var count = Count(File.ReadLines(file), Path.GetFileName(file) == "Manifest.cs");

            if (count > 0)
            {
                result[Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')] = count;
            }
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");

        return result;
    }

    private static int Count(IEnumerable<string> lines, bool isManifest)
    {
        var count = 0;

        foreach (var line in lines)
        {
            if (isManifest)
            {
                if (ManifestFeatureLine().IsMatch(line))
                {
                    count++;
                }
            }
            else if (FeatureAttribute().IsMatch(line))
            {
                count += FeatureIdLiteral().Count(line);
            }
        }

        return count;
    }

    [GeneratedRegex(@"\[(?:assembly:\s*)?(?:Feature|RequireFeatures)\(")]
    private static partial Regex FeatureAttribute();

    [GeneratedRegex(@"""(?:CrestApps\.)?OrchardCore\.[\w.]+""")]
    private static partial Regex FeatureIdLiteral();

    // A dependency in a manifest's list, or a manifest's Id, named with a literal.
    [GeneratedRegex(@"^\s*(?:Id\s*=\s*)?""(?:CrestApps\.)?OrchardCore\.[\w.]+""\s*,?\s*$")]
    private static partial Regex ManifestFeatureLine();

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
