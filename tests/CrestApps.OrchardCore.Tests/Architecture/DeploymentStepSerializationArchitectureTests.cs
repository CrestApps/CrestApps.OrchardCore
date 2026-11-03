using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Checkout.Deployments;
using CrestApps.OrchardCore.Shipping.Deployments;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A deployment plan saves each of its steps with System.Text.Json. Orchard Core's <see cref="DeploymentStep"/>
/// marks <c>Category</c> and <c>Title</c> with <c>[JsonIgnore]</c>, but System.Text.Json reads the attributes of the
/// most-derived override, so an override without its own <c>[JsonIgnore]</c> is serialized. Both are a
/// <c>LocalizationSource</c>, which holds a <see cref="Type"/>, so saving the plan throws and Admin &gt; Deployment
/// Plans &gt; Add Step answers 500. These guards fail when a CrestApps step serializes either member again.
/// </summary>
public sealed class DeploymentStepSerializationArchitectureTests
{
    private static readonly Regex _localizedOverrideRegex = new(
        @"override\s+LocalizationSource\s+(?<member>Category|Title)\b",
        RegexOptions.Compiled);

    // The C# source folders under src; the docs site is left out because its node_modules is large and holds no steps.
    private static readonly string[] _sourceFolders = ["Abstractions", "Core", "Modules", "Startup", "Targets", "Themes"];

    public static IEnumerable<object[]> DeploymentSteps()
        => DiscoverDeploymentStepTypes().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(DeploymentSteps))]
    public void DeploymentStep_SerializesWithoutCategoryOrTitle(Type stepType)
    {
        // Arrange
        var step = (DeploymentStep)Activator.CreateInstance(stepType, nonPublic: true);

        // Act
        var node = JsonSerializer.SerializeToNode(step, stepType);

        // Assert
        Assert.NotNull(node);
        var members = node.AsObject().Select(property => property.Key).ToArray();
        Assert.DoesNotContain(members, member => string.Equals(member, nameof(DeploymentStep.Category), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, member => string.Equals(member, nameof(DeploymentStep.Title), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KnownDeploymentSteps_AreCovered()
    {
        // Guards the discovery itself, so a refactor that stops finding steps cannot make the theory vacuous.
        var discovered = DiscoverDeploymentStepTypes().ToArray();

        Assert.Contains(typeof(AIPersonaDeploymentStep), discovered);
        Assert.Contains(typeof(CouponsDeploymentStep), discovered);
        Assert.Contains(typeof(ShippingZonesDeploymentStep), discovered);
    }

    [Fact]
    public void LocalizedOverrides_InSource_AreJsonIgnored()
    {
        // Covers steps in modules the test project does not reference, which the reflection theory cannot load.
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        var scannedOverrideCount = 0;

        // Act
        var sourceFiles = _sourceFolders
            .Select(folder => Path.Combine(repositoryRoot, "src", folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories));

        foreach (var fullPath in sourceFiles)
        {
            if (IsBuildOutput(fullPath))
            {
                continue;
            }

            var lines = File.ReadAllLines(fullPath);

            for (var index = 0; index < lines.Length; index++)
            {
                var match = _localizedOverrideRegex.Match(lines[index]);

                if (!match.Success)
                {
                    continue;
                }

                scannedOverrideCount++;

                if (!HasJsonIgnore(lines, index))
                {
                    var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
                    violations.Add($"{relativePath}:{index + 1} ({match.Groups["member"].Value})");
                }
            }
        }

        // Assert
        Assert.True(
            scannedOverrideCount > 0,
            "No LocalizationSource Category or Title overrides were found under src, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            "These deployment step overrides are missing [System.Text.Json.Serialization.JsonIgnore], so saving a " +
            $"deployment plan that contains the step fails: {string.Join(", ", violations)}.");
    }

    // The attribute sits on the lines just above the member, among its other attributes and doc comments.
    private static bool HasJsonIgnore(string[] lines, int memberIndex)
    {
        for (var index = memberIndex - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();

            if (!line.StartsWith('[') && !line.StartsWith("///", StringComparison.Ordinal))
            {
                return false;
            }

            if (line.StartsWith('[') && line.Contains("JsonIgnore", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> DiscoverDeploymentStepTypes()
    {
        // Touch the known step assemblies so their modules are certain to be present in the output.
        _ = typeof(AIPersonaDeploymentStep).Assembly;
        _ = typeof(CouponsDeploymentStep).Assembly;
        _ = typeof(ShippingZonesDeploymentStep).Assembly;

        var testAssemblyName = typeof(DeploymentStepSerializationArchitectureTests).Assembly.GetName().Name;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(name, testAssemblyName, StringComparison.OrdinalIgnoreCase) || !seen.Add(name))
            {
                continue;
            }

            Assembly assembly;

            try
            {
                assembly = Assembly.Load(AssemblyName.GetAssemblyName(path));
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
            {
                continue;
            }

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type.IsAbstract
                    || type.ContainsGenericParameters
                    || !typeof(DeploymentStep).IsAssignableFrom(type)
                    || type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) is null)
                {
                    continue;
                }

                yield return type;
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null);
        }
    }

    private static bool IsBuildOutput(string fullPath)
    {
        var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
