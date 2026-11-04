using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Holds the project layers apart (see <c>AGENTS.md</c>, "Layering and project references"):
/// <list type="bullet">
/// <item>a <c>.Core</c> library never references a module project;</item>
/// <item>an <c>.Abstractions</c> library never references a <c>.Core</c> library or a module project;</item>
/// <item>module code never uses a type from another module assembly, tag helpers and view components in compiled Razor
/// views included. A module may keep a <c>&lt;ProjectReference&gt;</c> to another module, only so the NuGet package of
/// the feature it depends on comes along; anything two modules share lives in a <c>.Core</c> or <c>.Abstractions</c>
/// project.</item>
/// </list>
/// The first two rules are checked on the project files. The third is checked on the compiled module assemblies: the
/// compiler drops a reference that nothing uses, so an assembly reference from one module to another means real use.
/// The violations that existed when the ratchet was set are listed below; the lists may only shrink.
/// </summary>
public sealed class LayeringRatchetTests
{
    /// <summary>
    /// Project references that broke the first two rules when the ratchet was set. Never add an entry; remove one as soon
    /// as its reference is gone.
    /// </summary>
    private static readonly HashSet<string> _knownProjectViolations = new(StringComparer.Ordinal)
    {
    };

    /// <summary>
    /// Module assemblies that used types of another module assembly when the ratchet was set. Never add an entry: move the
    /// type into a <c>.Core</c> or <c>.Abstractions</c> project instead. Remove an entry as soon as the use is gone.
    /// </summary>
    private static readonly HashSet<string> _knownModuleUses = new(StringComparer.Ordinal)
    {
        "CrestApps.OrchardCore.AI -> CrestApps.OrchardCore.AI.Prompting",
        "CrestApps.OrchardCore.AI.A2A -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Claude -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Copilot -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.DataSources -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Documents -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Documents.Azure -> CrestApps.OrchardCore.AI.Documents",
        "CrestApps.OrchardCore.AI.Mcp -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Memory.AzureAI -> CrestApps.OrchardCore.AI.Memory",
        "CrestApps.OrchardCore.AI.WebMcp -> CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.ContactCenter -> CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.PointOfSale -> CrestApps.OrchardCore.Orders",
    };

    private static readonly Lazy<List<ModuleUse>> _moduleUses = new(ReadModuleUses);

    [Fact]
    public void NoCoreOrAbstractionsProject_AddsAReferenceThatBreaksTheLayers()
    {
        // Arrange
        var violations = FindProjectViolations();

        // Act
        var added = violations
            .Where(violation => !_knownProjectViolations.Contains(violation.Reference))
            .Select(violation => $"{violation.Reference}: {violation.Rule}")
            .ToList();

        // Assert
        Assert.True(added.Count == 0, BuildMessage(
            "These project references break the layering rules in AGENTS.md (\"Layering and project references\"). " +
            "Move the type you need into a .Core or .Abstractions project and reference that instead:",
            added));
    }

    [Fact]
    public void EveryKnownProjectViolation_StillOccurs()
    {
        // Arrange
        var actual = FindProjectViolations()
            .Select(violation => violation.Reference)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var stale = _knownProjectViolations
            .Where(reference => !actual.Contains(reference))
            .ToList();

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(
            $"These references are gone. Delete their entries from {nameof(LayeringRatchetTests)}._knownProjectViolations so they cannot come back:",
            stale));
    }

    [Fact]
    public void NoModuleAssembly_StartsUsingTypesOfAnotherModule()
    {
        // Act
        var added = _moduleUses.Value
            .Where(use => !_knownModuleUses.Contains(use.Reference))
            .Select(use => $"{use.Reference} (uses {string.Join(", ", use.Types)})")
            .ToList();

        // Assert
        Assert.True(added.Count == 0, BuildMessage(
            "Module code must not use types of another module assembly, tag helpers and view components included. " +
            "Move these types into a .Core or .Abstractions project and reference that instead:",
            added));
    }

    [Fact]
    public void EveryKnownModuleUse_StillOccurs()
    {
        // Arrange
        var uses = _moduleUses.Value;
        var actual = uses
            .Select(use => use.Reference)
            .ToHashSet(StringComparer.Ordinal);
        var read = ReadableModuleAssemblies();

        // Act
        // A module that is not built in this run cannot be checked, so only a module whose assembly was read can prove
        // that an entry is stale.
        var stale = _knownModuleUses
            .Where(reference => read.Contains(reference[..reference.IndexOf(" -> ", StringComparison.Ordinal)]) && !actual.Contains(reference))
            .ToList();

        // Assert
        Assert.True(read.Count > 90, $"Only {read.Count} module assemblies were found; the scan no longer finds the build output.");
        Assert.True(stale.Count == 0, BuildMessage(
            $"These modules no longer use the other module. Delete their entries from {nameof(LayeringRatchetTests)}._knownModuleUses so the use cannot come back:",
            stale));
    }

    private static List<(string Reference, string Rule)> FindProjectViolations()
    {
        var projects = FindProjects();
        var layers = projects.ToDictionary(project => project.Name, project => project.Layer, StringComparer.Ordinal);
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

    private static List<(string Name, string Layer, string Path)> FindProjects()
    {
        var sourceRoot = Path.Combine(ProjectReferenceRulesTests.FindRepositoryRoot(), "src");
        var projects = new List<(string Name, string Layer, string Path)>();

        foreach (var layer in new[] { "Abstractions", "Core", "Modules" })
        {
            foreach (var project in Directory.EnumerateFiles(Path.Combine(sourceRoot, layer), "*.csproj", SearchOption.AllDirectories))
            {
                if (!IsBuildOutput(project))
                {
                    projects.Add((Path.GetFileNameWithoutExtension(project), layer, project));
                }
            }
        }

        Assert.True(projects.Count > 150, $"Only {projects.Count} projects were found under src; the scan no longer matches the repository layout.");

        return projects;
    }

    private static HashSet<string> ModuleNames()
        => FindProjects()
            .Where(project => project.Layer == "Modules")
            .Select(project => project.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ReadableModuleAssemblies()
        => ModuleNames()
            .Where(module => FindModuleAssembly(module) is not null)
            .ToHashSet(StringComparer.Ordinal);

    private static List<ModuleUse> ReadModuleUses()
    {
        var modules = ModuleNames();
        var uses = new List<ModuleUse>();

        foreach (var module in modules)
        {
            var path = FindModuleAssembly(module);

            if (path is null)
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();
            var referenced = new Dictionary<AssemblyReferenceHandle, string>();

            foreach (var handle in metadata.AssemblyReferences)
            {
                var name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);

                if (modules.Contains(name) && !string.Equals(name, module, StringComparison.Ordinal))
                {
                    referenced[handle] = name;
                }
            }

            if (referenced.Count == 0)
            {
                continue;
            }

            var types = referenced.Values.ToDictionary(name => name, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

            foreach (var handle in metadata.TypeReferences)
            {
                var type = metadata.GetTypeReference(handle);

                if (type.ResolutionScope.Kind == HandleKind.AssemblyReference &&
                    referenced.TryGetValue((AssemblyReferenceHandle)type.ResolutionScope, out var owner))
                {
                    types[owner].Add($"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}");
                }
            }

            foreach (var (name, used) in types)
            {
                uses.Add(new ModuleUse($"{module} -> {name}", [.. used]));
            }
        }

        return uses;
    }

    // The test project copies the modules it references next to itself; a module it does not reference is read from its
    // own build output, when it was built in the same configuration.
    private static string FindModuleAssembly(string module)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDirectory, module + ".dll");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        var targetFramework = new DirectoryInfo(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = targetFramework.Parent;

        if (configuration is null)
        {
            return null;
        }

        candidate = Path.Combine(ProjectReferenceRulesTests.FindRepositoryRoot(), "src", "Modules", module, "bin", configuration.Name, targetFramework.Name, module + ".dll");

        return File.Exists(candidate) ? candidate : null;
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

    private sealed record ModuleUse(string Reference, IReadOnlyList<string> Types);
}
