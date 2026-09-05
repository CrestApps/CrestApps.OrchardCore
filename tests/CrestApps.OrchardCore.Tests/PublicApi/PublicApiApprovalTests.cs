using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using PublicApiGenerator;

namespace CrestApps.OrchardCore.Tests.PublicApi;

/// <summary>
/// Locks the public surface of the assemblies another module compiles against.
/// </summary>
/// <remarks>
/// Everything an assembly exposes publicly is a promise to whoever compiles against it, and the Contact Center layer is
/// compiled against by other modules in this repository and by whatever a deployment adds beside them. Without a
/// recorded surface, a type going public, a member changing shape, or a class losing <c>sealed</c> reads in review as
/// an ordinary edit rather than as the compatibility decision it is. The generated surface is checked in, so a change
/// to it arrives as a diff a reviewer has to accept on purpose.
/// </remarks>
public sealed class PublicApiApprovalTests
{
    /// <summary>
    /// The assemblies whose public surface is recorded, named by a type each of them declares.
    /// </summary>
    /// <remarks>
    /// Assemblies are named through a type rather than through a string so that renaming or removing one breaks the
    /// build here instead of quietly dropping an assembly out of the recorded set.
    /// </remarks>
    public static TheoryData<string> GovernedAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (var name in GetGovernedAssemblyNames())
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GovernedAssemblies))]
    public void PublicSurface_MatchesTheApprovedBaseline(string assemblyName)
    {
        // Arrange
        var assembly = GetGovernedAssembly(assemblyName);
        var baselinePath = Path.Combine(GetBaselineDirectory(), $"{assemblyName}.approved.txt");

        // Act
        var actual = Normalize(assembly.GeneratePublicApi(new ApiGeneratorOptions
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

    /// <remarks>
    /// The baseline records that these hold today, but it records them as text, and text is easy to accept in bulk.
    /// Stating them as rules means a surface change that breaks one arrives as a named failure rather than as one more
    /// line in a diff someone is scrolling through.
    /// </remarks>
    /// <summary>
    /// Fails when a recorded surface contains something that only exists on the machine that recorded it.
    /// </summary>
    /// <remarks>
    /// A baseline is only a gate if every machine produces the same one. An attribute that carries a build path makes
    /// the recorded surface differ on every clone and on every CI run, so the gate fails for everyone and the only way
    /// to make it pass is to overwrite it - which is exactly the unread bulk acceptance the baseline exists to stop.
    /// The rule is stated here rather than left to the exclusion list, because the next attribute to carry a path will
    /// not be on that list.
    /// </remarks>
    [Theory]
    [MemberData(nameof(GovernedAssemblies))]
    public void ApprovedBaseline_ContainsNothingSpecificToTheMachineThatWroteIt(string assemblyName)
    {
        // Arrange
        var baselinePath = Path.Combine(GetBaselineDirectory(), $"{assemblyName}.approved.txt");

        Assert.True(File.Exists(baselinePath), $"No approved public surface is recorded for '{assemblyName}'.");

        var repositoryRoot = GetRepositoryRoot();

        // Act
        var offending = File
            .ReadAllLines(baselinePath)
            .Where(line => line.Contains(repositoryRoot, StringComparison.OrdinalIgnoreCase) ||
                line.Contains("C:\\", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("/home/", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("/Users/", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        // Assert
        Assert.True(
            offending.Count == 0,
            $"The recorded surface of '{assemblyName}' contains a filesystem path, so it can only match on the machine " +
            $"that wrote it and will fail for every other clone and on CI. Exclude the attribute that carries it." +
            $"{Environment.NewLine}    {string.Join($"{Environment.NewLine}    ", offending)}");
    }

    [Theory]
    [MemberData(nameof(GovernedAssemblies))]
    public void PublicClasses_AreSealedUnlessTheyAreDeliberateExtensionPoints(string assemblyName)
    {
        // Arrange
        var assembly = GetGovernedAssembly(assemblyName);

        var candidates = assembly
            .GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .ToList();

        // Act
        var violations = candidates
            .Where(type => !type.IsSealed && !IsDeliberatelyInheritable(type))
            .Select(type => $"{type.FullName} is public and can be inherited from, but declares nothing to override.")
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(candidates.Count > 0, $"No public class was read from {assemblyName}, so this gate proves nothing.");

        Assert.True(
            violations.Count == 0,
            $"A public class is open to inheritance by omission rather than by intent. Seal it, make it abstract, or give " +
            $"it an overridable member so that inheriting from it is a supported thing to do rather than an accident of " +
            $"the keyword it was missing.{Environment.NewLine}    {string.Join($"{Environment.NewLine}    ", violations)}");
    }

    /// <summary>
    /// Decides whether an unsealed public class is an extension point rather than a class that was never sealed.
    /// </summary>
    /// <param name="type">The public class to judge.</param>
    /// <remarks>
    /// A class that introduces something overridable is inviting derivation and has to keep that member's behaviour
    /// stable. Overriding a member it inherited is not the same statement - a driver that overrides one framework
    /// method has said nothing about whether anyone may derive from it - so a method only counts when this type is
    /// where it becomes virtual. Members the compiler synthesizes are excluded for the same reason: a record's
    /// generated equality members would otherwise make every public record inheritable by definition. A view model is
    /// unsealed because the display framework builds a runtime proxy from it, which it cannot do from a sealed type.
    /// Anything else that is unsealed is unsealed because nobody typed the keyword, and that is the case worth
    /// failing on.
    /// </remarks>
    private static bool IsDeliberatelyInheritable(Type type)
    {
        if (type.Namespace?.EndsWith(".ViewModels", StringComparison.Ordinal) == true)
        {
            return true;
        }

        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        return type
            .GetMethods(Declared)
            .Any(method =>
                method.IsVirtual &&
                !method.IsFinal &&
                !method.IsPrivate &&
                !method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) &&
                method.GetBaseDefinition().DeclaringType == type);
    }

    /// <summary>
    /// Asks a collection instance whether it refuses to be modified.
    /// </summary>
    /// <param name="value">The collection to ask.</param>
    /// <param name="runtimeType">The concrete type of <paramref name="value"/>.</param>
    /// <remarks>
    /// Asking the instance rather than reading its declared type is what separates a list handed out behind a
    /// read-only interface, which a caller can cast back and change, from a wrapper that genuinely refuses every
    /// mutating call. Returns <c>null</c> when the instance exposes no collection contract to ask, in which case the
    /// caller reports it rather than assuming either answer.
    /// </remarks>
    private static bool? IsReadOnlyCollection(object value, Type runtimeType)
    {
        foreach (var contract in runtimeType.GetInterfaces())
        {
            if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(ICollection<>))
            {
                continue;
            }

            var isReadOnly = contract.GetProperty(nameof(ICollection<int>.IsReadOnly));

            if (isReadOnly?.GetValue(value) is bool result)
            {
                return result;
            }
        }

        if (value is System.Collections.IList list)
        {
            return list.IsReadOnly;
        }

        return null;
    }

    private static bool LooksLikeACollection(Type type)
        => type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// Fails a static collection whose contents can be changed by whoever receives it.
    /// </summary>
    /// <param name="violations">The list each failure is added to.</param>
    /// <param name="member">The member being judged, named for the failure message.</param>
    /// <param name="read">Reads the member's current value.</param>
    /// <remarks>
    /// The declared type is not the question. <c>readonly</c> freezes a reference and not what it points at, and a
    /// read-only interface promises only that the caller was not handed a mutating method - the instance behind an
    /// <c>IReadOnlyList&lt;T&gt;</c> is usually a <c>List&lt;T&gt;</c>, and a caller can cast it back and rewrite
    /// shared state every tenant in the process reads. The value itself is therefore inspected, and only a genuinely
    /// immutable or frozen collection passes. A value that cannot be read is reported rather than skipped, because
    /// skipping is how a sweep quietly stops covering the member that needed it.
    /// </remarks>
    private static void Inspect(List<string> violations, string member, Func<object> read)
    {
        object value;

        try
        {
            value = read();
        }
        catch (Exception exception)
        {
            violations.Add($"{member} could not be read, so whether it is mutable could not be established: {exception.GetBaseException().Message}");

            return;
        }

        if (value is null)
        {
            return;
        }

        var runtimeType = value.GetType();

        if (runtimeType.IsArray)
        {
            violations.Add(
                $"{member} hands out a {runtimeType.Name}. An array reports itself as read-only while still letting any " +
                $"holder assign to its elements, so neither readonly nor a read-only interface protects it. Use an " +
                $"immutable or frozen collection.");

            return;
        }

        var runtimeNamespace = (runtimeType.IsGenericType ? runtimeType.GetGenericTypeDefinition() : runtimeType).Namespace;

        if (runtimeNamespace?.StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true ||
            runtimeNamespace?.StartsWith("System.Collections.Frozen", StringComparison.Ordinal) == true)
        {
            return;
        }

        var readOnly = IsReadOnlyCollection(value, runtimeType);

        if (readOnly == true)
        {
            return;
        }

        violations.Add(readOnly == false
            ? $"{member} hands out a {runtimeType.Name} that anything holding it can rewrite, which neither readonly nor a " +
                $"read-only interface prevents. Use an immutable or frozen collection."
            : $"{member} hands out a {runtimeType.Name}, which does not say whether its contents can be changed, so it " +
                $"cannot be shared as static state. Use an immutable or frozen collection.");
    }

    [Theory]
    [MemberData(nameof(GovernedAssemblies))]
    public void PublicTypes_ExposeNoMutableStaticState(string assemblyName)
    {
        // Arrange
        var assembly = GetGovernedAssembly(assemblyName);
        var violations = new List<string>();

        // Act
        foreach (var type in assembly.GetExportedTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsLiteral && !field.IsInitOnly)
                {
                    violations.Add($"{type.FullName}.{field.Name} is a public static field that anything can reassign.");
                }
                else if (field.IsInitOnly && LooksLikeACollection(field.FieldType))
                {
                    Inspect(violations, $"{type.FullName}.{field.Name}", () => field.GetValue(null));
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (property.SetMethod?.IsPublic == true)
                {
                    violations.Add($"{type.FullName}.{property.Name} is a public static property that anything can reassign.");
                }
                else if (property.GetMethod?.IsPublic == true &&
                    property.GetIndexParameters().Length == 0 &&
                    LooksLikeACollection(property.PropertyType))
                {
                    Inspect(violations, $"{type.FullName}.{property.Name}", () => property.GetValue(null));
                }
            }
        }

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Public static state is shared by every tenant in the process and by every test in the run, so a value one " +
            $"of them changes is a value all of them see.{Environment.NewLine}    {string.Join($"{Environment.NewLine}    ", violations.OrderBy(violation => violation, StringComparer.Ordinal))}");
    }

    [Fact]
    public void EveryGovernedAssembly_HasAnApprovedBaseline()
    {
        // Arrange
        var directory = GetBaselineDirectory();
        var governed = GetGovernedAssemblyNames().ToHashSet(StringComparer.Ordinal);

        // Act
        var recorded = Directory
            .EnumerateFiles(directory, "*.approved.txt")
            .Select(path => Path.GetFileName(path).Replace(".approved.txt", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.True(governed.Count > 0, "No assembly is governed, so this gate proves nothing.");

        var orphaned = recorded.Except(governed, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.True(
            orphaned.Count == 0,
            $"A baseline is recorded for an assembly that is no longer governed, so it is no longer compared against " +
            $"anything: {string.Join(", ", orphaned)}.");
    }

    /// <summary>
    /// Projects that reference every module in order to package or host them, rather than in order to compile against one.
    /// </summary>
    /// <remarks>
    /// These reference everything by design, so counting them as consumers would make every module look like a contract
    /// and would leave the rule saying nothing about which surfaces other code actually depends on.
    /// </remarks>
    private static readonly HashSet<string> _aggregators = new(StringComparer.Ordinal)
    {
        "CrestApps.OrchardCore.Cms.Core.Targets",
        "CrestApps.OrchardCore.Cms.Web",
    };

    private static readonly Regex _familyRegex = new(
        @"^CrestApps\.OrchardCore\.(ContactCenter|Telephony|Omnichannel)(\..+)?$",
        RegexOptions.Compiled);

    private static readonly Regex _typeDeclarationRegex = new(
        @"^\s*(?:\[[^\]]*\]\s*)*public\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|ref\s+)*(?:class|interface|struct|enum|record)\s+(?<name>[\w<>, \.]+)",
        RegexOptions.Compiled);

    private static readonly Regex _projectReferenceRegex = new(
        @"<ProjectReference\s+[^>]*Include\s*=\s*""(?<path>[^""]+\.csproj)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Loads a governed assembly from the build output beside this test.
    /// </summary>
    /// <param name="assemblyName">The simple name of the assembly to load.</param>
    /// <remarks>
    /// Resolution is by file rather than by <see cref="Assembly.Load(string)"/> so that an assembly the test project
    /// does not reference fails by saying which reference is missing, instead of throwing a file-not-found out of the
    /// data provider and taking every other case in this class down with it.
    /// </remarks>
    private static Assembly GetGovernedAssembly(string assemblyName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");

        Assert.True(
            File.Exists(path),
            $"'{assemblyName}' is compiled against by another project and is therefore governed, but it is not in this " +
            $"test project's output, so its public surface cannot be read. Add a ProjectReference to it from " +
            $"tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj.");

        return Assembly.LoadFrom(path);
    }

    /// <summary>
    /// Resolves the assemblies whose surface is recorded, from the rule that decides which surfaces are contracts.
    /// </summary>
    /// <remarks>
    /// A hand-written list is a list someone has to remember to add to. Deriving the set means a new project in the
    /// Contact Center, Telephony or Omnichannel families starts being governed the moment something compiles against
    /// it, rather than the moment somebody notices.
    /// </remarks>
    private static List<string> GetGovernedAssemblyNames()
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var family = new HashSet<string>(StringComparer.Ordinal);

        foreach (var projectPath in Directory.EnumerateFiles(Path.Combine(GetRepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var consumer = Path.GetFileNameWithoutExtension(projectPath);

            if (_familyRegex.IsMatch(consumer))
            {
                family.Add(consumer);
            }

            if (_aggregators.Contains(consumer))
            {
                continue;
            }

            foreach (Match match in _projectReferenceRegex.Matches(File.ReadAllText(projectPath)))
            {
                var referenced = Path.GetFileNameWithoutExtension(match.Groups["path"].Value.Replace('\\', '/'));

                if (!string.Equals(referenced, consumer, StringComparison.Ordinal))
                {
                    consumed.Add(referenced);
                }
            }
        }

        Assert.True(family.Count > 0, "No project was found in the governed families, so the rule read nothing.");

        var governed = family.Where(consumed.Contains).ToList();

        Assert.True(
            governed.Count > 0,
            "No project in the governed families is compiled against by another project, which cannot be true while the " +
            "Contact Center module builds on Telephony and Omnichannel.");

        Assert.True(
            governed.Count < family.Count,
            "Every project in the governed families counted as a contract, which means the rule stopped telling contracts " +
            $"apart from leaf modules. Families: {string.Join(", ", family.OrderBy(name => name, StringComparer.Ordinal))}.");

        return governed;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    /// <summary>
    /// Attributes that describe how an assembly was built rather than what it exposes.
    /// </summary>
    /// <remarks>
    /// These carry a version, a build configuration or a compiler detail, so leaving them in would make the recorded
    /// surface change on builds that changed nothing about the surface, and a gate that cries wolf is one reviewers
    /// learn to overwrite without reading.
    /// </remarks>
    private static readonly string[] _excludedAttributes =
    [
        "System.Diagnostics.DebuggerDisplayAttribute",
        "System.Diagnostics.DebuggerStepThroughAttribute",
        "System.Reflection.AssemblyMetadataAttribute",
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Runtime.Versioning.TargetFrameworkAttribute",

        // Emitted once per view, and each one carries the absolute path the view was compiled from, so leaving them
        // in would record the build machine's directory layout as though it were public surface and make the
        // baseline unreproducible anywhere but the machine that wrote it.
        "Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute",
        "OrchardCore.Modules.Manifest.ModuleAssetAttribute",
    ];

    private static string Normalize(string api)
    {
        return api.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    /// <remarks>
    /// Lines are qualified by the type that encloses them before they are compared, because roughly half of every
    /// baseline is text that repeats across types - <c>public long DocumentId { get; set; }</c> alone appears more
    /// than twenty times in one of them. Comparing the raw text would report the removal of one of those members as a
    /// change with nothing listed under it, which tells a reviewer that something moved without telling them what,
    /// and a reviewer who cannot see what changed accepts the file unread.
    /// </remarks>
    private static string Describe(string approved, string actual)
    {
        var approvedLines = Qualify(approved);
        var actualLines = Qualify(actual);

        var removed = Subtract(approvedLines, actualLines);
        var added = Subtract(actualLines, approvedLines);

        var builder = new StringBuilder();

        Append(builder, "No longer public", removed);
        Append(builder, "Newly public", added);

        return builder.ToString();
    }

    private static List<string> Qualify(string api)
    {
        var qualified = new List<string>();
        var scope = string.Empty;

        foreach (var line in api.Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var declaration = _typeDeclarationRegex.Match(line);

            if (declaration.Success)
            {
                scope = declaration.Groups["name"].Value;
            }

            qualified.Add(scope.Length == 0 ? line.Trim() : $"{scope}: {line.Trim()}");
        }

        return qualified;
    }

    /// <remarks>
    /// Occurrences are counted rather than set-subtracted, so removing one of several identical members is reported
    /// as one removal instead of vanishing because an identical line survives somewhere else.
    /// </remarks>
    private static List<string> Subtract(List<string> left, List<string> right)
    {
        var remaining = right
            .GroupBy(line => line, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var result = new List<string>();

        foreach (var line in left)
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
            {
                remaining[line] = count - 1;

                continue;
            }

            result.Add(line);
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

        foreach (var line in lines.OrderBy(line => line, StringComparer.Ordinal).Take(40))
        {
            builder.AppendLine($"    {line.Trim()}");
        }

        if (lines.Count > 40)
        {
            builder.AppendLine($"    ... and {lines.Count - 40} more.");
        }
    }

    private static string GetBaselineDirectory()
    {
        var baselines = Path.Combine(
            GetRepositoryRoot(),
            "tests",
            "CrestApps.OrchardCore.Tests",
            "PublicApi",
            "Baselines");

        Directory.CreateDirectory(baselines);

        return baselines;
    }
}
