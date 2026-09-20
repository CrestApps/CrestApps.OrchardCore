using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrestApps.Core.Hosting.Background;

/// <summary>
/// Provides extension methods for registering background cycles and the runner that drives them.
/// </summary>
public static class BackgroundCycleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a cycle and a runner that runs it on a schedule.
    /// </summary>
    /// <remarks>
    /// For a host with no scheduler of its own. Configure the schedule with
    /// <c>services.Configure&lt;BackgroundCycleOptions&gt;(CycleRunner&lt;TCycle&gt;.CycleName, ...)</c>.
    /// </remarks>
    /// <typeparam name="TCycle">The cycle contract.</typeparam>
    /// <typeparam name="TImplementation">The implementation.</typeparam>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddBackgroundCycleWorker<TCycle, TImplementation>(this IServiceCollection services)
        where TCycle : class, IBackgroundCycle
        where TImplementation : class, TCycle
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddBackgroundCycle<TCycle, TImplementation>();
        services.AddOptions<BackgroundCycleOptions>(CycleRunner<TCycle>.CycleName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CycleRunner<TCycle>>());

        return services;
    }
}
