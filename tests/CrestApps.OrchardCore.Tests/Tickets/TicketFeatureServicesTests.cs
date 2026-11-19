using System.Reflection;
using CrestApps.OrchardCore.Tickets.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using ManifestFeature = OrchardCore.Modules.Manifest.FeatureAttribute;
using StartupFeature = OrchardCore.Modules.FeatureAttribute;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// On a tenant with only Tickets on (and the features its manifest depends on), every controller, display driver and
/// service the feature registers can be built: each CrestApps service a constructor takes is registered by Tickets or
/// one of its dependencies. Orchard Core's and the framework's own services are the host's. Without this, a missing
/// registration only shows when another feature that happens to register it is off (the number sequence was).
/// </summary>
public sealed class TicketFeatureServicesTests
{
    [Fact]
    public void WithOnlyTicketsAndItsDependencies_EveryControllerDriverAndServiceCanBeBuilt()
    {
        // Arrange
        var services = new ServiceCollection();
        var assemblies = new List<Assembly>();
        Collect(typeof(TicketsController).Assembly.GetName().Name, assemblies);

        foreach (var assembly in assemblies)
        {
            foreach (var startup in BaseStartups(assembly))
            {
                startup.ConfigureServices(services);
            }
        }

        var roots = typeof(TicketsController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && (typeof(ControllerBase).IsAssignableFrom(type) || IsDisplayDriver(type)))
            // A controller or driver of another feature of the module (the portal) is checked with that feature.
            .Where(type => type.GetCustomAttribute<StartupFeature>() is not { } feature || feature.FeatureName == typeof(TicketsController).Assembly.GetName().Name)
            // A driver of an optional integration (Work Orders, Time Tracker) is built only with that feature on.
            .Where(type => type.GetCustomAttribute<RequireFeaturesAttribute>() is null)
            .Concat(services.Select(descriptor => descriptor.ImplementationType).Where(type => type is not null && IsOurs(type)))
            .Distinct()
            .ToList();

        // Act
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Type>();

        foreach (var root in roots)
        {
            Walk(root, services, visited, missing, root.Name);
        }

        // Assert
        Assert.True(roots.Count > 20, "The walk found too few types to be meaningful.");
        Assert.Empty(missing);
    }

    // The module and every CrestApps module its features depend on, transitively.
    private static void Collect(string assemblyName, List<Assembly> assemblies)
    {
        Assembly assembly;

        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (FileNotFoundException)
        {
            return;
        }

        if (assemblies.Contains(assembly))
        {
            return;
        }

        assemblies.Add(assembly);

        var mainFeature = assembly.GetCustomAttributes<ManifestFeature>().FirstOrDefault(feature => feature.Id == assemblyName)
            ?? assembly.GetCustomAttributes<ManifestFeature>().FirstOrDefault();

        foreach (var dependency in mainFeature?.Dependencies ?? [])
        {
            if (dependency.StartsWith("CrestApps.", StringComparison.Ordinal))
            {
                Collect(dependency, assemblies);
            }
        }
    }

    // The startups that run for a module's main feature with nothing optional on.
    private static IEnumerable<IStartup> BaseStartups(Assembly assembly)
    {
        var localizers = new ServiceCollection()
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

        foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IStartup).IsAssignableFrom(type)))
        {
            if (type.GetCustomAttribute<RequireFeaturesAttribute>() is not null)
            {
                continue;
            }

            var feature = type.GetCustomAttribute<StartupFeature>();

            if (feature is not null && feature.FeatureName != assembly.GetName().Name)
            {
                continue;
            }

            yield return (IStartup)ActivatorUtilities.CreateInstance(localizers, type);
        }
    }

    private static void Walk(Type type, IServiceCollection services, HashSet<Type> visited, SortedSet<string> missing, string path)
    {
        if (!visited.Add(type))
        {
            return;
        }

        var constructor = type.GetConstructors().OrderByDescending(item => item.GetParameters().Length).FirstOrDefault();

        foreach (var parameter in constructor?.GetParameters() ?? [])
        {
            var wanted = parameter.ParameterType;

            // A list may be empty, and the framework's own wrappers are the host's.
            if (wanted.IsGenericType && wanted.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                continue;
            }

            var registrations = services.Where(descriptor => descriptor.ServiceType == wanted
                || (wanted.IsGenericType && descriptor.ServiceType == wanted.GetGenericTypeDefinition())).ToList();

            if (registrations.Count == 0)
            {
                if (IsOurs(wanted) && !parameter.HasDefaultValue)
                {
                    missing.Add($"{path} needs {wanted.FullName}, which nothing registers.");
                }

                continue;
            }

            foreach (var registration in registrations.Where(descriptor => descriptor.ImplementationType is not null && IsOurs(descriptor.ImplementationType)))
            {
                var implementation = registration.ImplementationType;

                if (implementation.IsGenericTypeDefinition && wanted.IsGenericType)
                {
                    implementation = implementation.MakeGenericType(wanted.GetGenericArguments());
                }

                Walk(implementation, services, visited, missing, $"{path} > {implementation.Name}");
            }
        }
    }

    private static bool IsOurs(Type type)
        => type.Namespace?.StartsWith("CrestApps.", StringComparison.Ordinal) == true;

    private static bool IsDisplayDriver(Type type)
        => type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDisplayDriver<>));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
