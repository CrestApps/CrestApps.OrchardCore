using System.Reflection;
using System.Text;
using PublicApiGenerator;

namespace CrestApps.Core.ContactCenter.Tests.PublicApi;

/// <summary>
/// Records the public surface of every project that is being extracted into a package.
/// </summary>
/// <remarks>
/// <para>
/// These assemblies are what the extraction produces, so their surface is a package surface: once it ships,
/// it stops being ours to change quietly. Recording it while it is still free to change is the point, and it
/// is recorded here rather than beside the host because this is the only project that references them.
/// </para>
/// <para>
/// A surface that is empty is recorded as empty. Several of these projects have nothing in them yet, and a
/// baseline that says so is how the first thing added to one arrives as a reviewed change rather than as a
/// file appearing.
/// </para>
/// </remarks>
public sealed class FrameworkPublicApiTests
{
    /// <summary>
    /// Attributes left out of a recorded surface.
    /// </summary>
    /// <remarks>
    /// Each of these describes how the assembly was built rather than what it offers, and two of them carry
    /// the path of the machine that built it, which would make the baseline differ on every clone.
    /// </remarks>
    private static readonly string[] _excludedAttributes =
    [
        "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
        "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
        "System.Runtime.Versioning.TargetFrameworkAttribute",
        "System.Reflection.AssemblyCompanyAttribute",
        "System.Reflection.AssemblyConfigurationAttribute",
        "System.Reflection.AssemblyDescriptionAttribute",
        "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        "System.Reflection.AssemblyMetadataAttribute",
        "System.Reflection.AssemblyProductAttribute",
        "System.Reflection.AssemblyTitleAttribute",
        "System.Reflection.AssemblyVersionAttribute",
        "System.Diagnostics.DebuggableAttribute",
    ];

    public static TheoryData<string> ExtractedAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (var name in GetExtractedAssemblyNames())
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExtractedAssemblies))]
    public void PublicSurface_MatchesTheApprovedBaseline(string assemblyName)
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");

        Assert.True(
            File.Exists(path),
            $"'{assemblyName}' is being extracted and is therefore governed, but it is not in this test project's " +
            "output, so its surface cannot be read. Add a ProjectReference to it from " +
            "tests/Transitions/CrestApps.Core.ContactCenter.Tests/CrestApps.Core.ContactCenter.Tests.csproj.");

        var baselinePath = Path.Combine(GetBaselineDirectory(), $"{assemblyName}.approved.txt");

        // Act
        var actual = Normalize(Assembly.LoadFrom(path).GeneratePublicApi(new ApiGeneratorOptions
        {
            ExcludeAttributes = _excludedAttributes,
        }));

        // Assert
        if (!File.Exists(baselinePath))
        {
            File.WriteAllText(baselinePath, actual);

            Assert.Fail(
                $"No approved public surface existed for '{assemblyName}'. One has been written to " +
                $"'{baselinePath}'. Read it, decide whether every member on it is meant to be public, and commit it.");
        }

        var approved = Normalize(File.ReadAllText(baselinePath));

        if (string.Equals(approved, actual, StringComparison.Ordinal))
        {
            return;
        }

        var receivedPath = Path.Combine(GetBaselineDirectory(), $"{assemblyName}.received.txt");

        File.WriteAllText(receivedPath, actual);

        Assert.Fail(
            $"The public surface of '{assemblyName}' no longer matches its approved baseline.{Environment.NewLine}" +
            $"{Describe(approved, actual)}{Environment.NewLine}" +
            $"If every change above is intended, replace '{baselinePath}' with '{receivedPath}' and commit it, so the " +
            "change to the surface is reviewed as a change to the surface.");
    }

    [Fact]
    public void EveryRecordedBaseline_BelongsToAnAssemblyStillBeingExtracted()
    {
        // A baseline for an assembly nobody governs is compared against nothing, and reads as coverage that
        // does not exist.
        var governed = GetExtractedAssemblyNames().ToHashSet(StringComparer.Ordinal);

        var orphaned = Directory
            .EnumerateFiles(GetBaselineDirectory(), "*.approved.txt")
            .Select(file => Path.GetFileNameWithoutExtension(file).Replace(".approved", string.Empty, StringComparison.Ordinal))
            .Where(name => !governed.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            orphaned.Count == 0,
            $"These baselines are compared against nothing: {string.Join(", ", orphaned)}.");
    }

    /// <summary>
    /// The assemblies being extracted, read from the folders they are staged in.
    /// </summary>
    /// <remarks>
    /// Derived rather than listed, so a project added to the extraction starts being governed when it is
    /// created rather than when somebody remembers to add it here.
    /// </remarks>
    /// <returns>The assembly names.</returns>
    private static List<string> GetExtractedAssemblyNames()
    {
        var root = FindRepositoryRoot();

        var names = new[] { Path.Combine("src", "Abstractions", "Transitions"), Path.Combine("src", "Core", "Transitions") }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(names);

        return names;
    }

    private static string GetBaselineDirectory()
    {
        var directory = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "Transitions",
            "CrestApps.Core.ContactCenter.Tests",
            "PublicApi",
            "Baselines");

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static string Normalize(string api)
        => api.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>
    /// Describes what moved between two surfaces.
    /// </summary>
    /// <remarks>
    /// Lines are qualified by the type that encloses them before they are compared, because much of a baseline
    /// is text that repeats across types. Comparing the raw text reports a removal with nothing listed under
    /// it, and a reviewer who cannot see what changed accepts the file unread.
    /// </remarks>
    /// <param name="approved">The approved surface.</param>
    /// <param name="actual">The surface now.</param>
    /// <returns>A description of the difference.</returns>
    private static string Describe(string approved, string actual)
    {
        var approvedLines = Qualify(approved);
        var actualLines = Qualify(actual);

        var builder = new StringBuilder();

        Append(builder, "No longer public", Subtract(approvedLines, actualLines));
        Append(builder, "Newly public", Subtract(actualLines, approvedLines));

        return builder.ToString();
    }

    private static List<string> Qualify(string api)
    {
        var qualified = new List<string>();
        var scope = string.Empty;

        foreach (var line in api.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("public", StringComparison.Ordinal) &&
                (trimmed.Contains(" class ", StringComparison.Ordinal) ||
                 trimmed.Contains(" interface ", StringComparison.Ordinal) ||
                 trimmed.Contains(" struct ", StringComparison.Ordinal) ||
                 trimmed.Contains(" enum ", StringComparison.Ordinal)))
            {
                scope = trimmed;
            }

            qualified.Add(scope.Length == 0 ? trimmed : $"{scope} :: {trimmed}");
        }

        return qualified;
    }

    private static List<string> Subtract(List<string> from, List<string> other)
    {
        var remaining = new List<string>(other);
        var result = new List<string>();

        foreach (var line in from)
        {
            if (!remaining.Remove(line))
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static void Append(StringBuilder builder, string heading, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{heading} ({lines.Count}):");

        foreach (var line in lines.Order(StringComparer.Ordinal).Take(40))
        {
            builder.AppendLine($"    {line}");
        }

        if (lines.Count > 40)
        {
            builder.AppendLine($"    ... and {lines.Count - 40} more.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "The repository root was not found, so this rule reads nothing.");

        return directory.FullName;
    }
}
