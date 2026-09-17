using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.DependencyInjection;

/// <summary>
/// Renders a tenant's service descriptors as stable text that a baseline can record.
/// </summary>
/// <remarks>
/// The key is chosen so that moving a registration from a <c>Startup</c> into an <c>AddCore*</c>
/// extension method is not a difference, while the things that change behaviour are.
/// <para>
/// Descriptors are grouped by service type and sorted, rather than recorded at their position in the
/// collection. That is deliberate: Orchard Core discovers module startups in an order that varies
/// between runs, so any position recorded here - absolute, or relative within a service type that
/// more than one startup contributes to - differs from run to run and would fail this gate at random
/// without ever catching a defect. What this pins is the set: which services exist, how many
/// registrations each has, their lifetimes and their implementation types.
/// </para>
/// <para>
/// The identity of a factory delegate is deliberately not recorded, because a lambda's
/// compiler-generated name changes when it moves file even though it registers the same thing. The
/// blind spot that leaves is small and closed by resolving the affected services and asserting the
/// concrete type that comes back.
/// </para>
/// </remarks>
internal static class ServiceDescriptorSnapshot
{
    /// <summary>
    /// Renders every descriptor in the collection, grouped by service type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The rendered descriptors.</returns>
    public static string Render(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var groups = new Dictionary<string, List<ServiceDescriptor>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var descriptor in services)
        {
            var key = DescribeService(descriptor);

            // Only what this suite registers. Orchard Core discovers its own module startups in an
            // order that varies between runs, so its internal registrations shuffle their relative
            // positions from one run to the next. Nothing this refactor does can change that order,
            // and including it would bury a real regression under noise that is never actionable.
            if (!Owned(key) && !Owned(DescribeImplementation(descriptor)))
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups[key] = group;
                order.Add(key);
            }

            group.Add(descriptor);
        }

        var builder = new StringBuilder();

        foreach (var key in order.OrderBy(value => value, StringComparer.Ordinal))
        {
            // Sorted, not in registration order. Registration order is only stable within a single
            // startup; across startups it follows Orchard Core's module discovery, which varies
            // between runs. Recording it would fail this gate at random without ever catching a
            // defect. What this snapshot pins is therefore the set: which services exist, how many
            // registrations each has, their lifetimes and their implementation types.
            //
            // Resolution order, which is what actually decides IEnumerable behaviour and which
            // registration wins, is pinned separately and more directly by
            // ServiceResolutionOrderTests, which asks a real tenant container what it hands back.
            var rendered = groups[key]
                .Select(descriptor => (descriptor.Lifetime, Implementation: DescribeImplementation(descriptor)))
                .OrderBy(entry => entry.Implementation, StringComparer.Ordinal)
                .ThenBy(entry => entry.Lifetime);

            var index = 0;

            foreach (var entry in rendered)
            {
                builder
                    .Append(key)
                    .Append(" #")
                    .Append(index++.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(entry.Lifetime)
                    .Append(" -> ")
                    .AppendLine(entry.Implementation);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets whether a rendered description names something this suite owns.
    /// </summary>
    /// <remarks>
    /// Matching anywhere in the description rather than only at the start is deliberate: it keeps
    /// registrations such as <c>IConfigureOptions</c> of a CrestApps options type, or a catalog of a
    /// CrestApps model, which name an Orchard Core service type but exist only because this suite
    /// asked for them.
    /// </remarks>
    /// <param name="description">The rendered description.</param>
    /// <returns><see langword="true"/> when the description names a CrestApps type.</returns>
    private static bool Owned(string description)
        => description.Contains("CrestApps", StringComparison.Ordinal);

    /// <summary>
    /// Describes the service a descriptor registers, including its key when it is a keyed service.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The service description.</returns>
    private static string DescribeService(ServiceDescriptor descriptor)
    {
        var service = Describe(descriptor.ServiceType);

        return descriptor.IsKeyedService
            ? service + " key:" + (descriptor.ServiceKey?.ToString() ?? "<null>")
            : service;
    }

    /// <summary>
    /// Describes what a descriptor resolves to.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The implementation description.</returns>
    private static string DescribeImplementation(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            if (descriptor.KeyedImplementationType is not null)
            {
                return "type:" + Describe(descriptor.KeyedImplementationType);
            }

            return descriptor.KeyedImplementationInstance is not null
                ? "instance:" + Describe(descriptor.KeyedImplementationInstance.GetType())
                : "factory";
        }

        if (descriptor.ImplementationType is not null)
        {
            return "type:" + Describe(descriptor.ImplementationType);
        }

        return descriptor.ImplementationInstance is not null
            ? "instance:" + Describe(descriptor.ImplementationInstance.GetType())
            : "factory";
    }

    /// <summary>
    /// Describes a type by a name that does not change between machines or runs.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The type name.</returns>
    private static string Describe(Type type)
    {
        if (type is null)
        {
            return "<null>";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tick = definition.IndexOf('`', StringComparison.Ordinal);

        if (tick >= 0)
        {
            definition = definition[..tick];
        }

        // An open generic registration such as ICatalog<> has generic parameters rather than
        // arguments; rendering those by name would leak the parameter letter, so they collapse to <>.
        var rendered = type.ContainsGenericParameters
            ? string.Empty
            : string.Join(",", type.GetGenericArguments().Select(Describe));

        return definition + "<" + rendered + ">";
    }
}
