using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tickets.Core;

/// <summary>
/// Registers the services of tickets.
/// </summary>
public static class TicketsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ticket service, the access service and the service-level scope of tickets.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddTicketsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ITicketService, DefaultTicketService>();
        services.TryAddScoped<ITicketAccessService, DefaultTicketAccessService>();
        services.TryAddScoped<TicketAutoCloser>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITeamMemberLoadSource, TicketTeamMemberLoadSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketEmailObserver>());
        services.AddTemplatedEmailTemplateSource<TicketEmailTemplateSource>();
        services.AddServiceLevelScope<TicketServiceLevelScope>();

        return services;
    }
}
