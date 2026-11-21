using CrestApps.OrchardCore.Learning.Lti.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the extension points of External Tools.
/// </summary>
public static class LtiServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of the scores tools post.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLtiScoreObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ILtiScoreObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILtiScoreObserver, TObserver>());

        return services;
    }
}
