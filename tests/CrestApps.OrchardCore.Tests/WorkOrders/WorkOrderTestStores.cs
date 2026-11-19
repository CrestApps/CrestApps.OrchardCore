using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// What a test store needs for work orders: their tables and those of the shared service levels and timeline they keep
/// their timers and notes in, and the services of all three.
/// </summary>
internal static class WorkOrderTestStores
{
    /// <summary>
    /// Gets the collections work orders and service levels are stored in.
    /// </summary>
    public static string[] Collections => [WorkOrdersConstants.CollectionName, ServiceLevelsConstants.CollectionName, TimelineConstants.CollectionName];

    /// <summary>
    /// Gets the index providers of work orders and service levels.
    /// </summary>
    public static IIndexProvider[] IndexProviders =>
    [
        new WorkOrderIndexProvider(),
        new WorkOrderTypeIndexProvider(),
        new ChecklistTemplateIndexProvider(),
        new WorkOrderSubStatusIndexProvider(),
        new ServiceLevelPolicyIndexProvider(),
        new ServiceLevelTimerIndexProvider(),
        new TimelineEntryIndexProvider(),
    ];

    /// <summary>
    /// Creates the index tables of work orders and service levels.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateSchemaAsync(ISchemaBuilder schemaBuilder)
    {
        await WorkOrdersIndexSchema.CreateAsync(schemaBuilder);
        await ServiceLevelsIndexSchema.CreateAsync(schemaBuilder);
        await TimelineIndexSchema.CreateAsync(schemaBuilder);
    }

    /// <summary>
    /// Registers the work order services with the service levels they keep their timers in.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkOrdersWithServiceLevels(this IServiceCollection services)
    {
        services.AddWorkOrdersCore();
        services.AddOpenHours();
        services.AddServiceLevelsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<CrestApps.Core.Services.ICatalogEntryHandler<CrestApps.OrchardCore.ServiceLevels.Models.ServiceLevelPolicy>, CrestApps.OrchardCore.ServiceLevels.Core.Handlers.ServiceLevelPolicyHandler>());
        services.AddTimelineCore();

        return services;
    }
}
