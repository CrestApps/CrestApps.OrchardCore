using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.Workforce;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the dispatch services.
/// </summary>
public static class DispatchServiceCollectionExtensions
{
    private static readonly Type _source = typeof(DispatchServiceCollectionExtensions);

    /// <summary>
    /// Adds the visit store, the candidate finder, the dispatch service and the job event type of Workforce
    /// calendars. Work orders, Workforce, Skills and Geo must be registered too.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDispatchCore(this IServiceCollection services)
    {
        services.TryAddScoped<ServiceVisitStore>();
        services.TryAddScoped<DispatchResources>();
        services.TryAddScoped<SlotFitter>();
        services.TryAddScoped<VisitCalendarWriter>();
        services.AddNumberSequence();
        services.TryAddScoped<IDispatchService, DefaultDispatchService>();
        services.TryAddScoped<IDispatchCandidateFinder, DefaultDispatchCandidateFinder>();
        services.TryAddScoped(sp => new Lazy<WorkOrderService>(sp.GetRequiredService<WorkOrderService>));
        services.TryAddScoped(sp => new Lazy<IDispatchService>(sp.GetRequiredService<IDispatchService>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkOrderObserver, DispatchWorkOrderObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDispatchOptimizer, DefaultDispatchOptimizer>());
        services.TryAddScoped<IDispatchOptimizationService, DispatchOptimizationService>();
        services.AddSignedLinks();
        services.TryAddScoped<IVisitTrackingLinks, VisitTrackingLinks>();
        services.TryAddScoped<VisitNoticeService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IVisitObserver, VisitNoticeObserver>());
        services.TryAddScoped<VisitRiskMonitor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkOrderHandler, DispatchWorkOrderHandler>());

        // A visit shows on each worker's team calendar as a job, which keeps them busy then.
        services.AddCalendarType(WorkforceConstants.CalendarType, type => type
            .AddEventType(DispatchConstants.JobEventType, LocalizationSource.Create("Job", _source), e => e.Color("#0c8599").Busy().IgnoresCapacity()));

        return services;
    }

    /// <summary>
    /// Adds the technician app's services: the field sync, location consent and positions, and their stores.
    /// The dispatch services must be registered too.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDispatchMobileCore(this IServiceCollection services)
    {
        services.AddOptions<DispatchMobileOptions>();
        services.TryAddScoped<FieldCommandReceiptStore>();
        services.TryAddScoped<WorkerLocationConsentStore>();
        services.TryAddScoped<WorkerLocationPingStore>();
        services.TryAddScoped<IWorkerLocationService, WorkerLocationService>();
        services.TryAddScoped<IFieldSyncService, FieldSyncService>();

        return services;
    }
}
