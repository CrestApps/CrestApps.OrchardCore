using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards the Contact Center tenant-isolation architecture boundaries.
/// </summary>
public sealed class ContactCenterArchitectureGuardTests
{
    private const string RawRedisRule = "Raw StackExchange.Redis usage";
    private const string StaticMutableStateRule = "Static mutable collection or ThreadStatic state";
    private const string TenantMemoryCacheRule = "Tenant-sensitive IMemoryCache usage";
    private const string AsteriskSubscribeAllRule = "Asterisk ARI subscribeAll enabled";

    private static readonly Regex _rawRedisUsageRegex = new(
        @"^\s*using\s+StackExchange\.Redis\s*;|\b(?:IConnectionMultiplexer|ConnectionMultiplexer)\b",
        RegexOptions.Compiled);

    private static readonly Regex _staticCollectionTypeRegex = new(
        @"\b(?:Dictionary|ConcurrentDictionary|List|HashSet)\s*<",
        RegexOptions.Compiled);

    private static readonly Regex _staticTokenRegex = new(
        @"\bstatic\b",
        RegexOptions.Compiled);

    private static readonly Regex _staticReadonlyOrConstRegex = new(
        @"\b(?:static\s+readonly|readonly\s+static|const)\b",
        RegexOptions.Compiled);

    private static readonly Regex _memoryCacheUsageRegex = new(
        @"\bIMemoryCache\b",
        RegexOptions.Compiled);

    private static readonly Regex _subscribeAllEnabledRegex = new(
        @"\bsubscribeAll\b\s*(?:=|:)\s*(?:true|bool\.TrueString(?:\.ToLowerInvariant\(\))?)|\[\s*""subscribeAll""\s*\]\s*=\s*(?:true|bool\.TrueString(?:\.ToLowerInvariant\(\))?)|\bSubscribeAll\b\s*=\s*true",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly ArchitectureAllowlistEntry[] _allowlist =
    [
        new ArchitectureAllowlistEntry(
            RawRedisRule,
            "src/Modules/CrestApps.OrchardCore.SignalR/RedisBackplaneStartup.cs",
            null,
            "Approved Orchard Core SignalR Redis backplane adapter; it tenant-prefixes the backplane channel."),
        new ArchitectureAllowlistEntry(
            StaticMutableStateRule,
            "src/Abstractions/CrestApps.OrchardCore.ContentTransfer.Abstractions/ContentTransferPermissions.cs",
            "_permissionsByType",
            "Global Orchard permission-template cache; it does not hold tenant data."),
    ];

    // TECH DEBT (Part 3 removal): pre-existing ARI subscribeAll=true occurrences.
    // Removed by the CC-1 7-step tenant event-ownership fix in Part 3, which deletes both the code AND these baseline entries.
    // This baseline keeps the guard green today while still failing on any NEW subscribeAll=true.
    private static readonly ArchitectureKnownBaselineEntry[] _subscribeAllKnownBaseline =
    [
        new ArchitectureKnownBaselineEntry(
            "src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskSettingsUtilities.cs",
            "[\"subscribeAll\"] = bool.TrueString"),
        new ArchitectureKnownBaselineEntry(
            "src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskSettingsUtilities.cs",
            "[\"subscribeAll\"] = bool.TrueString"),
        new ArchitectureKnownBaselineEntry(
            "src/Startup/CrestApps.OrchardCore.Asterisk.Web/Services/AsteriskAriConnectionUtilities.cs",
            "[\"subscribeAll\"] = bool.TrueString"),
        new ArchitectureKnownBaselineEntry(
            "src/Startup/CrestApps.OrchardCore.Asterisk.Web/Services/AsteriskAriConnectionUtilities.cs",
            "[\"subscribeAll\"] = bool.TrueString"),
    ];

    /// <summary>
    /// Ensures application code does not bypass Orchard Core Redis primitives.
    /// </summary>
    [Fact]
    public void RawStackExchangeRedisUsage_WhenOutsideApprovedBackplane_DoesNotExist()
    {
        // Arrange
        var sourceFiles = EnumerateCSharpSourceFiles();

        // Act
        var violations = ScanLines(sourceFiles, RawRedisRule, line => _rawRedisUsageRegex.IsMatch(line))
            .Where(violation => !IsAllowlisted(violation))
            .ToList();

        // Assert
        AssertNoViolations(violations, "Use Orchard Core Redis primitives instead of raw StackExchange.Redis. The only approved adapter is explicitly allowlisted.");
    }

    /// <summary>
    /// Ensures tenant data is not stored in process-wide mutable static collections.
    /// </summary>
    [Fact]
    public void StaticMutableTenantState_WhenObviousStaticCollectionOrThreadStatic_DoesNotExist()
    {
        // Arrange
        var sourceFiles = EnumerateCSharpSourceFiles();

        // Act
        var violations = ScanLines(
            sourceFiles,
            StaticMutableStateRule,
            line => ContainsThreadStatic(line) || ContainsStaticMutableCollectionField(line))
            .Where(violation => !IsAllowlisted(violation))
            .ToList();

        // Assert
        AssertNoViolations(violations, "Use shell-scoped services or tenant-keyed IDistributedCache plus ISignal instead of static tenant state.");
    }

    /// <summary>
    /// Ensures tenant-sensitive modules do not use process-local memory caches for tenant data.
    /// </summary>
    [Fact]
    public void MemoryCacheUsage_WhenInTenantSensitiveModules_DoesNotExist()
    {
        // Arrange
        var sourceFiles = EnumerateCSharpSourceFiles()
            .Where(file => IsTenantSensitiveModulePath(file.RelativePath));

        // Act
        var violations = ScanLines(sourceFiles, TenantMemoryCacheRule, line => _memoryCacheUsageRegex.IsMatch(line))
            .Where(violation => !IsAllowlisted(violation))
            .ToList();

        // Assert
        AssertNoViolations(violations, "Use tenant-keyed IDistributedCache plus ISignal, or shell-scoped state, in Contact Center, Telephony, Asterisk, and Omnichannel code.");
    }

    /// <summary>
    /// Ensures Asterisk ARI configuration never subscribes a tenant listener to all PBX events.
    /// </summary>
    [Fact]
    public void AsteriskAriSubscribeAll_WhenEnabled_DoesNotExist()
    {
        // Arrange
        var sourceFiles = EnumerateApplicationConfigurationFiles()
            .Where(file => IsAsteriskPath(file.RelativePath));

        // Act
        var violations = ExceptKnownBaseline(
            ScanLines(sourceFiles, AsteriskSubscribeAllRule, line => _subscribeAllEnabledRegex.IsMatch(line)),
            _subscribeAllKnownBaseline)
            .Where(violation => !IsAllowlisted(violation))
            .ToList();

        // Assert
        AssertNoViolations(violations, "Set ARI subscribeAll to false and route events through tenant-owned provider bindings.");
    }

    private static List<ArchitectureSourceFile> EnumerateCSharpSourceFiles()
    {
        return EnumerateApplicationFiles("*.cs");
    }

    private static List<ArchitectureSourceFile> EnumerateApplicationConfigurationFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".json",
            ".yml",
            ".yaml",
            ".config",
            ".props",
            ".targets",
        };

        return Directory
            .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
            .Select(file => new ArchitectureSourceFile(file, ToRepositoryRelativePath(repositoryRoot, file)))
            .Where(file => !IsExcludedApplicationPath(file.RelativePath))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ArchitectureSourceFile> EnumerateApplicationFiles(string searchPattern)
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");

        return Directory
            .EnumerateFiles(sourceRoot, searchPattern, SearchOption.AllDirectories)
            .Select(file => new ArchitectureSourceFile(file, ToRepositoryRelativePath(repositoryRoot, file)))
            .Where(file => !IsExcludedApplicationPath(file.RelativePath))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ArchitectureViolation> ScanLines(
        IEnumerable<ArchitectureSourceFile> files,
        string rule,
        Func<string, bool> isViolation)
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file.FullPath);

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];

                if (IsCommentOnlyLine(line))
                {
                    continue;
                }

                if (isViolation(line))
                {
                    violations.Add(new ArchitectureViolation(rule, file.RelativePath, index + 1, line.Trim()));
                }
            }
        }

        return violations;
    }

    private static bool ContainsThreadStatic(string line)
    {
        return line.Contains("[ThreadStatic]", StringComparison.Ordinal);
    }

    private static bool ContainsStaticMutableCollectionField(string line)
    {
        if (!_staticTokenRegex.IsMatch(line) || !_staticCollectionTypeRegex.IsMatch(line))
        {
            return false;
        }

        if (_staticReadonlyOrConstRegex.IsMatch(line))
        {
            return false;
        }

        var terminatorIndex = IndexOfFirstFieldTerminator(line);

        if (terminatorIndex < 0)
        {
            return false;
        }

        var declarationPrefix = line.Substring(0, terminatorIndex);

        return !declarationPrefix.Contains('(');
    }

    private static int IndexOfFirstFieldTerminator(string line)
    {
        var equalsIndex = line.IndexOf('=');
        var semicolonIndex = line.IndexOf(';');
        var braceIndex = line.IndexOf('{');
        var result = -1;

        foreach (var index in new[] { equalsIndex, semicolonIndex, braceIndex })
        {
            if (index >= 0 && (result < 0 || index < result))
            {
                result = index;
            }
        }

        return result;
    }

    private static bool IsAllowlisted(ArchitectureViolation violation)
    {
        return _allowlist.Any(entry => entry.Matches(violation));
    }

    private static IEnumerable<ArchitectureViolation> ExceptKnownBaseline(
        IEnumerable<ArchitectureViolation> violations,
        IReadOnlyList<ArchitectureKnownBaselineEntry> knownBaseline)
    {
        var suppressedBaselineEntries = new bool[knownBaseline.Count];

        foreach (var violation in violations)
        {
            var baselineIndex = FindFirstUnsuppressedBaselineIndex(violation, knownBaseline, suppressedBaselineEntries);

            if (baselineIndex < 0)
            {
                yield return violation;

                continue;
            }

            suppressedBaselineEntries[baselineIndex] = true;
        }
    }

    private static int FindFirstUnsuppressedBaselineIndex(
        ArchitectureViolation violation,
        IReadOnlyList<ArchitectureKnownBaselineEntry> knownBaseline,
        bool[] suppressedBaselineEntries)
    {
        for (var index = 0; index < knownBaseline.Count; index++)
        {
            if (!suppressedBaselineEntries[index] && knownBaseline[index].Matches(violation))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTenantSensitiveModulePath(string relativePath)
    {
        return relativePath.Contains(".ContactCenter", StringComparison.Ordinal)
            || relativePath.Contains(".Telephony", StringComparison.Ordinal)
            || relativePath.Contains(".Asterisk", StringComparison.Ordinal)
            || relativePath.Contains(".Omnichannel", StringComparison.Ordinal);
    }

    private static bool IsAsteriskPath(string relativePath)
    {
        return relativePath.Contains(".Asterisk", StringComparison.Ordinal);
    }

    private static bool IsExcludedApplicationPath(string relativePath)
    {
        var segments = relativePath.Split('/');

        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("node_modules", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("wwwroot", StringComparer.OrdinalIgnoreCase)
            || relativePath.StartsWith("src/CrestApps.Docs/", StringComparison.Ordinal);
    }

    private static bool IsCommentOnlyLine(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("///", StringComparison.Ordinal)
            || trimmed.StartsWith('*');
    }

    private static void AssertNoViolations(IReadOnlyList<ArchitectureViolation> violations, string guidance)
    {
        Assert.True(violations.Count == 0, CreateFailureMessage(violations, guidance));
    }

    private static string CreateFailureMessage(IReadOnlyList<ArchitectureViolation> violations, string guidance)
    {
        var builder = new StringBuilder();

        builder.AppendLine(guidance);
        builder.AppendLine("Architecture guard violations:");

        foreach (var violation in violations.OrderBy(violation => violation.RelativePath, StringComparer.Ordinal).ThenBy(violation => violation.LineNumber))
        {
            builder.Append("- ");
            builder.Append(violation.RelativePath);
            builder.Append(':');
            builder.Append(violation.LineNumber);
            builder.Append(" [");
            builder.Append(violation.Rule);
            builder.Append("] ");
            builder.AppendLine(violation.LineText);
        }

        builder.AppendLine("Allowlist entries must be explicit and include a justification.");

        return builder.ToString();
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

    private static string ToRepositoryRelativePath(string repositoryRoot, string fullPath)
    {
        return Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed class ArchitectureSourceFile
    {
        public ArchitectureSourceFile(string fullPath, string relativePath)
        {
            FullPath = fullPath;
            RelativePath = relativePath;
        }

        public string FullPath { get; }

        public string RelativePath { get; }
    }

    private sealed class ArchitectureViolation
    {
        public ArchitectureViolation(string rule, string relativePath, int lineNumber, string lineText)
        {
            Rule = rule;
            RelativePath = relativePath;
            LineNumber = lineNumber;
            LineText = lineText;
        }

        public string Rule { get; }

        public string RelativePath { get; }

        public int LineNumber { get; }

        public string LineText { get; }
    }

    private sealed class ArchitectureAllowlistEntry
    {
        public ArchitectureAllowlistEntry(string rule, string relativePath, string requiredLineFragment, string justification)
        {
            Rule = rule;
            RelativePath = relativePath;
            RequiredLineFragment = requiredLineFragment;
            Justification = justification;
        }

        public string Rule { get; }

        public string RelativePath { get; }

        public string RequiredLineFragment { get; }

        public string Justification { get; }

        public bool Matches(ArchitectureViolation violation)
        {
            return string.Equals(Rule, violation.Rule, StringComparison.Ordinal)
                && string.Equals(RelativePath, violation.RelativePath, StringComparison.Ordinal)
                && (RequiredLineFragment is null || violation.LineText.Contains(RequiredLineFragment, StringComparison.Ordinal));
        }
    }

    private sealed class ArchitectureKnownBaselineEntry
    {
        public ArchitectureKnownBaselineEntry(string relativePath, string requiredLineFragment)
        {
            RelativePath = relativePath;
            RequiredLineFragment = requiredLineFragment;
        }

        public string RelativePath { get; }

        public string RequiredLineFragment { get; }

        public bool Matches(ArchitectureViolation violation)
        {
            return string.Equals(RelativePath, violation.RelativePath, StringComparison.Ordinal)
                && violation.LineText.Contains(RequiredLineFragment, StringComparison.Ordinal);
        }
    }
}
