using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
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

        // The settings records, one document per record in the service desk's collection.
        services.TryAddScoped<ICatalog<TicketDesk>, TicketSettingsStore<TicketDesk, TicketDeskIndex>>();
        services.TryAddScoped<ICatalog<TicketStatus>, TicketSettingsStore<TicketStatus, TicketStatusIndex>>();
        services.TryAddScoped<ICatalog<TicketPriority>, TicketSettingsStore<TicketPriority, TicketPriorityIndex>>();
        services.TryAddScoped<ICatalog<TicketView>, TicketSettingsStore<TicketView, TicketViewIndex>>();
        services.TryAddScoped<ICatalog<TicketBoard>, TicketSettingsStore<TicketBoard, TicketBoardIndex>>();

        // Ticket numbers come from the shared number sequence; nothing else in the feature registers it.
        services.AddNumberSequence();
        services.TryAddScoped<ITicketService, DefaultTicketService>();
        services.TryAddScoped<ITicketAccessService, DefaultTicketAccessService>();
        services.TryAddScoped<ITicketBoardService, DefaultTicketBoardService>();
        services.TryAddScoped<ITicketProtectedInformation, DefaultTicketProtectedInformation>();
        services.TryAddScoped<TicketProtectedInformationCleaner>();
        services.TryAddScoped<ITicketCategoryService, TicketCategoryService>();
        services.TryAddScoped<TicketAutoCloser>();
        services.TryAddScoped<TicketBacklogRecorder>();
        services.TryAddScoped<TicketSettingsUsage>();
        services.TryAddScoped<TicketEmailOutbox>();
        services.TryAddScoped<TicketRatingRequests>();
        services.AddResumableStep<TicketSettingsChangesStep>();
        services.AddResumableStep<TicketReindexStep>();
        services.AddResumableStep<TicketCategoryPathsStep>();
        services.AddServiceLevelPolicyUsage<TicketSettingsUsage>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITeamMemberLoadSource, TicketTeamMemberLoadSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketEmailObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketProblemObserver>());
        services.TryAddScoped<TicketProblemCascade>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceLevelObserver, TicketServiceLevelObserver>());
        services.AddTemplatedEmailTemplateSource<TicketEmailTemplateSource>();
        services.AddServiceLevelScope<TicketServiceLevelScope>();
        services.AddServiceLevelTimerAccessProvider<TicketServiceLevelTimerAccess>();

        return services;
    }
}
