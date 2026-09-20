using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.Hosting.Background;

/// <summary>
/// Provides extension methods for registering background cycles.
/// </summary>
public static class BackgroundCycleRegistrationExtensions
{
    /// <summary>
    /// Registers a cycle, without anything to run it.
    /// </summary>
    /// <remarks>
    /// What a host with its own scheduler calls: the work is available to resolve, and the host
    /// decides when it happens. A host without a scheduler uses the framework runner instead, which
    /// registers the cycle as well.
    /// </remarks>
    /// <typeparam name="TCycle">The cycle contract.</typeparam>
    /// <typeparam name="TImplementation">The implementation.</typeparam>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddBackgroundCycle<TCycle, TImplementation>(this IServiceCollection services)
        where TCycle : class, IBackgroundCycle
        where TImplementation : class, TCycle
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TCycle, TImplementation>();

        return services;
    }
}
