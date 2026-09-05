using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Audits every Contact Center feature manifest against the services a tenant actually registers when that
/// feature is enabled.
/// </summary>
/// <remarks>
/// A feature manifest is a contract: enabling the feature must be sufficient to run it. Nothing in the build
/// enforces that contract, so a service can quietly acquire a constructor dependency owned by a feature its own
/// manifest never declares. The tenant still starts, because a container registration is not validated until
/// something resolves it. The failure surfaces later, on a customer tenant that did not happen to enable the
/// other feature.
/// <para>
/// The oracle is deliberately narrow so a failure is always a real defect. A service that is simply not
/// registered resolves to <see langword="null"/>, which is correct: the feature owning it is not enabled. A
/// service that is <em>registered and still cannot be constructed</em> is never correct, because some enabled
/// feature registered it while the feature owning its dependency was outside the declared closure. Only that
/// case is reported.
/// </para>
/// <para>
/// The feature list is discovered by reflection over the feature-id constants, so a feature added later is
/// audited without anyone remembering to extend this file. That is what closes the class of defect rather than
/// any single instance of it.
/// </para>
/// </remarks>
public sealed class ContactCenterFeatureDependencyAuditTests
{
    private static readonly string[] _auditedAssemblyPrefixes =
    [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Telephony",
    ];

    [Fact]
    public async Task EveryFeature_CanConstructEveryServiceItsOwnDependencyClosureRegisters()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var candidateServiceTypes = GetCandidateServiceTypes();

        Assert.NotEmpty(candidateServiceTypes);

        var violations = new List<string>();

        foreach (var featureId in GetContactCenterFeatureIds())
        {
            var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
            {
                Id = featureId,
                ProviderProfile = "none",
                Features = [featureId],
            });

            var unresolvable = await host.ExecuteInTenantScopeAsync(
                tenant,
                serviceProvider => Task.FromResult(
                    FindRegisteredButUnconstructableServices(serviceProvider, featureId, candidateServiceTypes)));

            violations.AddRange(unresolvable);
        }

        Assert.True(violations.Count == 0, Describe(
            "Services are registered that the enabling feature's declared dependency closure cannot construct.",
            "Declare the missing feature in the registering feature's manifest, or move the registration into the " +
            "feature that owns its dependency.",
            violations));
    }

    [Fact]
    public async Task EveryFeature_DeclaresDependenciesThatExist()
    {
        var violations = await AuditManifestsAsync(static (feature, availableIds, _) =>
        {
            var declared = feature.Dependencies.ToArray();
            var found = new List<string>();

            foreach (var dependencyId in declared.Where(id => !availableIds.Contains(id)))
            {
                found.Add($"'{feature.Id}' declares '{dependencyId}', which is not an available feature.");
            }

            foreach (var duplicate in declared.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                found.Add($"'{feature.Id}' declares '{duplicate.Key}' more than once.");
            }

            if (declared.Contains(feature.Id, StringComparer.Ordinal))
            {
                found.Add($"'{feature.Id}' declares itself as a dependency.");
            }

            return found;
        });

        Assert.True(violations.Count == 0, Describe(
            "Feature manifests declare dependencies that cannot be satisfied.",
            "Correct the manifest so every declared dependency names an available feature exactly once.",
            violations));
    }

    [Fact]
    public async Task NoFeature_DeclaresACircularDependency()
    {
        var violations = await AuditManifestsAsync(static (feature, _, dependenciesById) =>
        {
            var path = new List<string>();

            if (TryFindCycle(feature.Id, feature.Id, dependenciesById, [], path))
            {
                return [$"'{feature.Id}' depends on itself through {string.Join(" -> ", path)}."];
            }

            return [];
        });

        Assert.True(violations.Count == 0, Describe(
            "Feature manifests declare a dependency cycle.",
            "Break the cycle by moving the shared services into a feature both sides can depend on.",
            violations));
    }

    /// <summary>
    /// Boots a single tenant and applies the supplied manifest rule to every Contact Center feature.
    /// </summary>
    /// <param name="rule">The rule to apply, receiving the feature, the available feature identifiers and the declared dependency graph.</param>
    /// <returns>Every violation the rule reported.</returns>
    private static async Task<List<string>> AuditManifestsAsync(
        Func<IFeatureInfo, HashSet<string>, Dictionary<string, string[]>, IEnumerable<string>> rule)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "manifest-audit",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Area],
        });

        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = (await featureManager.GetAvailableFeaturesAsync()).ToArray();
            var availableIds = availableFeatures.Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
            var dependenciesById = availableFeatures.ToDictionary(
                feature => feature.Id,
                feature => feature.Dependencies.ToArray(),
                StringComparer.Ordinal);

            var contactCenterFeatureIds = GetContactCenterFeatureIds().ToHashSet(StringComparer.Ordinal);
            var violations = new List<string>();

            foreach (var feature in availableFeatures.Where(feature => contactCenterFeatureIds.Contains(feature.Id)))
            {
                violations.AddRange(rule(feature, availableIds, dependenciesById));
            }

            return violations;
        });
    }

    /// <summary>
    /// Finds every candidate service the tenant has a registration for but cannot construct.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider to resolve against.</param>
    /// <param name="featureId">The feature that was enabled, used to describe a violation.</param>
    /// <param name="candidateServiceTypes">The service contracts to probe.</param>
    /// <returns>A description of each registered but unconstructable service.</returns>
    private static List<string> FindRegisteredButUnconstructableServices(
        IServiceProvider serviceProvider,
        string featureId,
        Type[] candidateServiceTypes)
    {
        var violations = new List<string>();

        // Some HTTP-scoped services (for example anything that resolves IResourceManager) read the ambient
        // HttpContext at construction and are only ever resolved during a request in production. The audit
        // resolves them in a bare shell scope, so a request context is supplied here to keep the oracle focused
        // on genuine manifest gaps rather than reporting the absence of an HttpContext as an unconstructable
        // service.
        var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();

        if (httpContextAccessor is not null && httpContextAccessor.HttpContext is null)
        {
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
            };
        }

        foreach (var serviceType in candidateServiceTypes)
        {
            // The collection is probed as well as the contract, because a feature commonly contributes behaviour
            // by adding one handler to a collection another feature injects. A single unconstructable element
            // makes the whole collection throw, which is exactly the production symptom.
            foreach (var probedType in new[] { serviceType, typeof(IEnumerable<>).MakeGenericType(serviceType) })
            {
                try
                {
                    serviceProvider.GetService(probedType);
                }
                catch (Exception exception)
                {
                    violations.Add(
                        $"With only '{featureId}' enabled, '{probedType.FullName}' is registered but cannot be " +
                        $"constructed: {exception.Message}");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Walks the declared dependency graph looking for a path that returns to the starting feature.
    /// </summary>
    /// <param name="origin">The feature the search started from.</param>
    /// <param name="current">The feature currently being expanded.</param>
    /// <param name="dependenciesById">The declared dependencies of every available feature.</param>
    /// <param name="visited">The features already expanded on this search.</param>
    /// <param name="path">Receives the cycle path when one is found.</param>
    /// <returns><see langword="true"/> when the origin is reachable from itself.</returns>
    private static bool TryFindCycle(
        string origin,
        string current,
        Dictionary<string, string[]> dependenciesById,
        HashSet<string> visited,
        List<string> path)
    {
        if (!dependenciesById.TryGetValue(current, out var dependencies))
        {
            return false;
        }

        foreach (var dependencyId in dependencies)
        {
            path.Add(dependencyId);

            if (dependencyId == origin)
            {
                return true;
            }

            if (visited.Add(dependencyId) &&
                TryFindCycle(origin, dependencyId, dependenciesById, visited, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    /// <summary>
    /// Gets the service contracts the audit probes.
    /// </summary>
    /// <returns>The interfaces declared by the Contact Center and Telephony assemblies.</returns>
    /// <remarks>
    /// Interfaces are probed rather than implementation types because a manifest gap surfaces when something
    /// resolves a contract, and because <c>ITypeFeatureProvider</c> is not a sound oracle for "what did this
    /// feature register". It is populated from two sources, and only one of them is the container.
    /// <c>ShellContainerFactory.PopulateTypeFeatureProvider</c> adds the DI service descriptors, but
    /// <c>ExtensionManager</c> first harvests every public non-abstract class in the module assembly under the
    /// comment "Get all types from all extension and add them to the type feature provider", attributing each to
    /// the feature named by a type-level <c>[Feature]</c> attribute or, when there is none, to the
    /// module-named feature — and to <em>every</em> feature of the extension when that lookup misses. Because
    /// the later container pass uses a non-overwriting <c>TryAdd</c>, the map is the union of both, so it lists
    /// types belonging to features that are switched off and were never registered. Measured on this module, a
    /// tenant with only the base feature enabled attributed 147 types to that feature, including
    /// <c>ContactCenterRealTimeEventHandler</c>, whose registering startup never ran. An implementation-driven
    /// audit built on it would therefore report registrations that do not exist.
    /// </remarks>
    private static Type[] GetCandidateServiceTypes()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => _auditedAssemblyPrefixes.Any(prefix =>
                assembly.GetName().Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .SelectMany(GetLoadableTypes)
            .Where(type => type.IsInterface && !type.ContainsGenericParameters)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Gets the types an assembly exposes, tolerating types whose dependencies cannot be loaded.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>The types that could be loaded.</returns>
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).ToArray();
        }
    }

    /// <summary>
    /// Gets every feature identifier declared by the Contact Center module.
    /// </summary>
    /// <returns>The feature identifiers.</returns>
    /// <remarks>
    /// Reading the constants rather than a hand-maintained list is what makes this audit cover features added
    /// after it was written.
    /// </remarks>
    private static string[] GetContactCenterFeatureIds()
        => typeof(ContactCenterConstants.Feature)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue())
            .Where(featureId => !string.IsNullOrEmpty(featureId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(featureId => featureId, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Builds an assertion message that names every violation and explains how to resolve it.
    /// </summary>
    /// <param name="summary">What the audit found.</param>
    /// <param name="remedy">How to resolve the violations.</param>
    /// <param name="violations">The individual violations.</param>
    /// <returns>The assertion message.</returns>
    private static string Describe(string summary, string remedy, IEnumerable<string> violations)
    {
        var message = new StringBuilder(summary).AppendLine().AppendLine();

        foreach (var violation in violations)
        {
            message.Append("  - ").AppendLine(violation);
        }

        return message.AppendLine().Append(remedy).ToString();
    }
}
