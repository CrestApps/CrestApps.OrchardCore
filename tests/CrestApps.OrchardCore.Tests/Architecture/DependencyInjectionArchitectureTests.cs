using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards how contact-centre code obtains its collaborators. Two habits are refused because both hide a missing
/// registration until run time and in the wrong tenant.
/// <para>
/// The first is taking an <see cref="IServiceProvider"/> and resolving from it. A constructor that does this
/// tells the container nothing about what the type needs, so a tenant that has not enabled the feature that
/// supplies the collaborator still constructs the type happily and fails later, deep inside a call, usually on a
/// live interaction.
/// </para>
/// <para>
/// The second is injecting <c>IEnumerable&lt;TService&gt;</c> only to call <c>FirstOrDefault()</c> on it. That is
/// service location wearing a constructor parameter: it silently accepts zero implementations, and it silently
/// picks an arbitrary one when a second feature registers another. A contract that is genuinely optional gets a
/// null-object default registered by the feature that declares it, and is then injected like anything else.
/// </para>
/// Every exception is listed below with the reason it is an exception, because an unexplained exception is
/// indistinguishable from an oversight.
/// </summary>
public sealed class DependencyInjectionArchitectureTests
{
    private const string ServiceProviderRule = "IServiceProvider constructor injection";
    private const string OptionalEnumerableRule = "IEnumerable<T> injected only to take the first";

    /// <summary>
    /// The contracts that now have a null-object default, so no consumer has any reason to scan for them.
    /// </summary>
    private static readonly string[] _contractsWithNullObjectDefaults =
    [
        "IAgentWorkStateHealingService",
        "IBusinessHoursGate",
        "ICallbackService",
        "IDialerProfileReader",
        "IQueuedVoiceWorkOfferService",
    ];

    private static readonly Regex _constructorRegex = new(
        @"public\s+(?<name>[A-Z]\w*)\s*\((?<parameters>[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _serviceProviderParameterRegex = new(
        @"\bIServiceProvider\s+\w+",
        RegexOptions.Compiled);

    /// <summary>
    /// The types allowed to take an <see cref="IServiceProvider"/>, and why each one is not service location.
    /// </summary>
    private static readonly DependencyInjectionException[] _serviceProviderExceptions =
    [
        new DependencyInjectionException(
            "ContactCenterScopeExecutor",
            "Its whole purpose is to open a tenant scope and run work in it, so the container is the thing it operates on."),
        new DependencyInjectionException(
            "ContactCenterFeatureLifecycleHandler",
            "It runs while features are being enabled and disabled, when the set of registered services is exactly what is changing."),
        new DependencyInjectionException(
            "DefaultTelephonyProviderResolver",
            "It activates a provider type chosen at run time from the provider catalog, which is what a container activator is for."),
        new DependencyInjectionException(
            "ProviderVoiceOfferSynchronizationService",
            "It resolves the presence manager inside a scope it creates per synchronization pass, not from its own scope."),
        new DependencyInjectionException(
            "ActivityQueueHandler",
            "A queue handler that injected the queue-group manager would close a handler-to-manager-to-handler construction cycle."),
        new DependencyInjectionException(
            "RecordingMediaDeletionHandler",
            "An event handler that injected the event publisher would close a publisher-to-handler construction cycle."),
        new DependencyInjectionException(
            "ContactActivityExportHandler",
            "An import handler that injected the import manager would close a manager-to-handler construction cycle."),
        new DependencyInjectionException(
            "VoiceMediaItemDisplayDriver",
            "The provisioner is supplied by whichever telephony provider feature is enabled, and the driver renders differently when none is."),
    ];

    /// <summary>
    /// The remaining places that inject a collection and take the first element, with the reason each is not one
    /// of the optional contracts above. Adding to this list is a decision, not a formality.
    /// </summary>
    private static readonly DependencyInjectionException[] _optionalEnumerableExceptions =
    [
        new DependencyInjectionException(
            "_contractsWithNullObjectDefaults",
            "Only the contracts listed in that array are guarded; every other collection injection is out of this rule's scope until it, too, is given a default."),
    ];

    [Fact]
    public void ContactCenterCode_DoesNotResolveItsCollaboratorsFromTheContainer()
    {
        // Arrange
        var files = EnumerateInScopeSourceFiles();
        var violations = new List<string>();

        // Act
        foreach (var file in files)
        {
            var source = File.ReadAllText(file.FullPath);

            foreach (Match match in _constructorRegex.Matches(source))
            {
                if (!_serviceProviderParameterRegex.IsMatch(match.Groups["parameters"].Value))
                {
                    continue;
                }

                var typeName = match.Groups["name"].Value;

                if (IsExcepted(typeName, _serviceProviderExceptions))
                {
                    continue;
                }

                violations.Add($"{file.RelativePath}: {typeName}");
            }
        }

        // Assert
        AssertNoViolations(
            ServiceProviderRule,
            violations,
            "Inject the collaborator itself. When it is genuinely optional, register a null-object default in the " +
            "feature that declares the contract and replace it in the feature that implements it; when taking the " +
            "container is unavoidable, add the type to the exception list with the reason.");
    }

    [Fact]
    public void ContractsWithNullObjectDefaults_AreInjectedDirectly_NotScannedFor()
    {
        // Arrange
        var files = EnumerateInScopeSourceFiles();
        var violations = new List<string>();

        // Act
        foreach (var file in files)
        {
            var source = File.ReadAllText(file.FullPath);

            foreach (var contract in _contractsWithNullObjectDefaults)
            {
                if (source.Contains($"IEnumerable<{contract}>", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: IEnumerable<{contract}>");
                }

                if (source.Contains($"GetService<{contract}>", StringComparison.Ordinal))
                {
                    violations.Add($"{file.RelativePath}: GetService<{contract}>()");
                }
            }
        }

        // Assert
        AssertNoViolations(
            OptionalEnumerableRule,
            violations,
            "These contracts have a null-object default, so the collaborator is always resolvable. Take it as a " +
            "plain constructor parameter and delete the null check that went with the scan.");
    }

    [Fact]
    public void EveryException_NamesTheReasonItIsAnException()
    {
        // Assert
        // An exception list that is allowed to grow without reasons stops being a record of decisions and becomes
        // a place to put anything that fails.
        Assert.All(
            _serviceProviderExceptions.Concat(_optionalEnumerableExceptions),
            exception =>
            {
                Assert.False(string.IsNullOrWhiteSpace(exception.TypeName));
                Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
                Assert.True(exception.Reason.Length > 40, $"The reason recorded for '{exception.TypeName}' is too short to be one.");
            });
    }

    private static bool IsExcepted(string typeName, DependencyInjectionException[] exceptions)
        => exceptions.Any(exception => string.Equals(exception.TypeName, typeName, StringComparison.Ordinal))
            || typeName.EndsWith("ScopeContext", StringComparison.Ordinal)
            || typeName.EndsWith("BackgroundTask", StringComparison.Ordinal)
            || typeName.EndsWith("Hub", StringComparison.Ordinal);

    private static void AssertNoViolations(string rule, List<string> violations, string guidance)
    {
        if (violations.Count == 0)
        {
            return;
        }

        var message = new StringBuilder()
            .AppendLine($"{rule} — {violations.Count} violation(s):")
            .AppendLine();

        foreach (var violation in violations.OrderBy(violation => violation, StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(violation);
        }

        message.AppendLine().AppendLine(guidance);

        Assert.Fail(message.ToString());
    }

    private static List<SourceFile> EnumerateInScopeSourceFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");

        return Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => new SourceFile(file, Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')))
            .Where(file => IsInScope(file.RelativePath))
            .ToList();
    }

    private static bool IsInScope(string relativePath)
        => relativePath.Contains("ContactCenter", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("Telephony", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("Omnichannel", StringComparison.OrdinalIgnoreCase);

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

    private sealed record SourceFile(string FullPath, string RelativePath);

    private sealed record DependencyInjectionException(string TypeName, string Reason);
}
