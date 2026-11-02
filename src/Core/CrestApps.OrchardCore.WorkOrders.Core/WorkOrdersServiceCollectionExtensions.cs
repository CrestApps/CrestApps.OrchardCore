using CrestApps.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Work Orders services.
/// </summary>
public static class WorkOrdersServiceCollectionExtensions
{
    /// <summary>
    /// Registers the stores, managers, the service level clock and monitor, and the work order service.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddWorkOrdersCore(this IServiceCollection services)
    {
        services.TryAddScoped<WorkOrderStore>();
        services.TryAddScoped<ICatalog<WorkOrderType>, WorkOrderSettingStore<WorkOrderType, WorkOrderTypeIndex>>();
        services.TryAddScoped<ICatalog<ChecklistTemplate>, WorkOrderSettingStore<ChecklistTemplate, ChecklistTemplateIndex>>();
        services.TryAddScoped<ICatalog<ServiceLevelPolicy>, WorkOrderSettingStore<ServiceLevelPolicy, ServiceLevelPolicyIndex>>();
        services.TryAddScoped<ICatalog<WorkOrderSubStatus>, WorkOrderSettingStore<WorkOrderSubStatus, WorkOrderSubStatusIndex>>();
        services.TryAddScoped(typeof(WorkOrderSettingManager<>));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkOrderType>, WorkOrderTypeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ChecklistTemplate>, ChecklistTemplateHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceLevelPolicy>, ServiceLevelPolicyHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkOrderSubStatus>, WorkOrderSubStatusHandler>());
        services.TryAddScoped<SlaClock>();
        services.TryAddScoped<SlaMonitor>();
        services.TryAddScoped<WorkOrderService>();

        return services;
    }

    /// <summary>
    /// Registers service agreements: their store, manager and scheduler.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddServiceAgreementsCore(this IServiceCollection services)
    {
        services.TryAddScoped<ServiceAgreementStore>();
        services.TryAddScoped<ServiceAgreementManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceAgreement>, ServiceAgreementHandler>());
        services.TryAddScoped<ServiceAgreementScheduler>();

        return services;
    }
}
