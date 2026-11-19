using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Registration helpers for modules that build on tickets.
/// </summary>
public static class TicketsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of what happens to tickets.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTicketObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ITicketObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TObserver>());

        return services;
    }
}
