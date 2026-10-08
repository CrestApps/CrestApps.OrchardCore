using System.Reflection;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Locks layer 2 of the tenant hierarchy scope containment: in the tenant hierarchy assemblies, only a short, reviewed
/// list of types may take the services that reach other tenants, and only the broker may mark a call as a broker call.
/// </summary>
public sealed class TenantHierarchyArchitectureTests
{
    private static readonly Type[] _crossTenantServices =
    [
        typeof(IShellHost),
        typeof(IShellSettingsManager),
        typeof(IShellRemovalManager),
    ];

    private static readonly HashSet<string> _allowedTypes =
    [
        // The broker is the one code path to another tenant of a hierarchy.
        nameof(TenantHierarchyBroker),

        // The platform runs in the Default tenant, which may reach every tenant.
        nameof(TenantHierarchyPlatformService),

        // The host-level guards read the tenant list or decorate the host itself.
        nameof(GuardedShellHost),
        nameof(EgressGuard),
        "ParentRemovalTenantEvents",
        "ParentRemovalHostHandler",
    ];

    [Fact]
    public void OnlyReviewedTypes_TakeTheServicesThatReachOtherTenants()
    {
        // Arrange
        var assemblies = new[]
        {
            typeof(TenantHierarchyConstants).Assembly,
            typeof(TenantHierarchyBroker).Assembly,
            typeof(ChildTenantManager).Assembly,
        };

        // Act
        var offenders = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => _crossTenantServices.Contains(parameter.ParameterType)))
            .Where(type => !_allowedTypes.Contains(type.Name))
            .Select(type => type.FullName)
            .ToList();

        // Assert
        Assert.True(offenders.Count == 0, $"These types take a service that reaches other tenants: {string.Join(", ", offenders)}. Route the call through ITenantHierarchyBroker.");
    }

    [Fact]
    public void OnlyTheBroker_MarksABrokerCall()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var sources = new[]
        {
            Path.Combine(root, "src", "Abstractions", "CrestApps.OrchardCore.TenantHierarchy.Abstractions"),
            Path.Combine(root, "src", "Core", "CrestApps.OrchardCore.TenantHierarchy.Core"),
            Path.Combine(root, "src", "Modules", "CrestApps.OrchardCore.TenantHierarchy"),
        };

        // Act
        var offenders = sources
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("BrokerCallContext.Run", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(name => name is not "TenantHierarchyBroker.cs" and not "BrokerCallContext.cs")
            .ToList();

        // Assert
        Assert.True(offenders.Count == 0, $"Only the broker may mark a broker call: {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void ParentAndChildFeatures_DoNotDependOnTheDefaultOnlyPlatformOrTenantsFeatures()
    {
        // Arrange
        var features = typeof(ChildTenantManager).Assembly
            .GetCustomAttributes<global::OrchardCore.Modules.Manifest.FeatureAttribute>()
            .Where(feature => !string.IsNullOrEmpty(feature.Id))
            .ToDictionary(feature => feature.Id);

        // Act
        var parent = features[TenantHierarchyConstants.Features.Parent];
        var child = features[TenantHierarchyConstants.Features.Child];
        var platform = features[TenantHierarchyConstants.Features.Platform];

        // Assert: depending on a Default-only feature would make the feature unavailable everywhere else.
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Platform, parent.Dependencies);
        Assert.DoesNotContain(TenantHierarchyConstants.Features.Platform, child.Dependencies);
        Assert.DoesNotContain("OrchardCore.Tenants", parent.Dependencies);
        Assert.DoesNotContain("OrchardCore.Tenants", child.Dependencies);
        Assert.True(platform.DefaultTenantOnly);
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
