using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Records;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A <see cref="LocalizationSource"/> is a static declaration owned by code, never serialized or deserialized: it names
/// a <see cref="Type"/>, which can't be serialized, and a type can't be created safely from a name read from a document.
/// Orchard Core removes these properties from the contract of its own serializers, but a property marked with
/// <see cref="JsonIgnoreAttribute"/> also stays out of every other serializer. System.Text.Json doesn't apply the
/// attribute of a base or interface property to an implementation or an override, so each one needs its own. These
/// guards fail when an instance <see cref="LocalizationSource"/> property in a CrestApps assembly lacks it.
/// </summary>
public sealed class LocalizationSourceJsonIgnoreArchitectureTests
{
    private static readonly Regex _propertyRegex = new(
        @"^\s*(?:(?:public|protected|internal|private|override|virtual|abstract|sealed|required|new)\s+)*(?:global::)?(?:OrchardCore\.Localization\.)?LocalizationSource\??\s+(?<member>[A-Za-z_]\w*)\s*(?:\{|=>|$)",
        RegexOptions.Compiled);

    // The C# source folders under src; the docs site is left out because its node_modules is large and holds no code.
    private static readonly string[] _sourceFolders = ["Abstractions", "Core", "Modules", "Startup", "Targets", "Themes"];

    [Fact]
    public void LocalizationSourceInstanceProperties_AreMarkedWithJsonIgnore()
    {
        // Arrange
        var types = DiscoverTypes().ToList();

        // Act
        var missing = types
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(LocalizationSource))
                .Where(property => !property.IsDefined(typeof(JsonIgnoreAttribute), inherit: false))
                .Select(property => $"{type.FullName}.{property.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.Contains(typeof(IRecordSource).Assembly, types.Select(type => type.Assembly));
        Assert.True(
            missing.Count == 0,
            $"These LocalizationSource properties are missing [System.Text.Json.Serialization.JsonIgnore]: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void LocalizationSourceProperties_InSource_AreJsonIgnored()
    {
        // Covers modules the test project does not reference, which the reflection test cannot load.
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        var scannedPropertyCount = 0;

        // Act
        var sourceFiles = _sourceFolders
            .Select(folder => Path.Combine(repositoryRoot, "src", folder))
            .Where(Directory.Exists)
            .SelectMany(folder => SourceTreeFiles.Enumerate(folder, "*.cs", directory => SourceTreeFiles.IsBuildOutputDirectory(directory) || Path.GetFileName(directory) == "node_modules"));

        foreach (var fullPath in sourceFiles)
        {
            if (IsBuildOutput(fullPath))
            {
                continue;
            }

            var text = File.ReadAllText(fullPath);

            if (!text.Contains(nameof(LocalizationSource), StringComparison.Ordinal))
            {
                continue;
            }

            var lines = text.ReplaceLineEndings("\n").Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];

                // Static properties are not serialized.
                if (line.Contains("static ", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = _propertyRegex.Match(line);

                if (!match.Success)
                {
                    continue;
                }

                scannedPropertyCount++;

                if (!HasJsonIgnore(lines, index))
                {
                    var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
                    violations.Add($"{relativePath}:{index + 1} ({match.Groups["member"].Value})");
                }
            }
        }

        // Assert
        Assert.True(
            scannedPropertyCount > 0,
            "No LocalizationSource properties were found under src, so the guard would pass vacuously.");
        Assert.True(
            violations.Count == 0,
            $"These LocalizationSource properties are missing [System.Text.Json.Serialization.JsonIgnore]: {string.Join(", ", violations)}.");
    }

    // The attribute sits on the lines just above the member, among its other attributes and comments.
    private static bool HasJsonIgnore(string[] lines, int memberIndex)
    {
        for (var index = memberIndex - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();

            if (!line.StartsWith('[') && !line.StartsWith("//", StringComparison.Ordinal))
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

    private static IEnumerable<Type> DiscoverTypes()
    {
        // Touch a known assembly so it is certain to be present in the output.
        _ = typeof(IRecordSource).Assembly;

        var testAssemblyName = typeof(LocalizationSourceJsonIgnoreArchitectureTests).Assembly.GetName().Name;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(path);

            // The test assemblies declare types of their own.
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
                // The properties of an interface don't apply to its implementations.
                if (!type.IsInterface)
                {
                    yield return type;
                }
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
