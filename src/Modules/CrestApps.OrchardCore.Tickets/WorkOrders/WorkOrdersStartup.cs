using CrestApps.OrchardCore.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes.Workflows;
using CrestApps.OrchardCore.Tickets.Workflows;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Tickets.WorkOrders;

/// <summary>
/// With Work Orders on, a ticket becomes a work order when someone has to go on site, and hears when it is done.
/// </summary>
[RequireFeatures(WorkOrdersConstants.Features.WorkOrders)]
public sealed class WorkOrdersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TicketWorkOrders>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkOrderObserver, TicketWorkOrderObserver>());
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketWorkOrderDisplayDriver>();
    }
}

/// <summary>
/// The <b>Create Work Order From Ticket</b> workflow task.
/// </summary>
[RequireFeatures(WorkOrdersConstants.Features.WorkOrders, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkOrdersWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddActivity<CreateTicketWorkOrderTask, TicketTaskDisplayDriver<CreateTicketWorkOrderTask>>();
}

/// <summary>
/// The recipe schema of the <b>Create Work Order From Ticket</b> workflow task.
/// </summary>
[RequireFeatures(WorkOrdersConstants.Features.WorkOrders, OrchardCoreFeatureIds.Workflows, CrestAppsRecipesConstants.Feature.Area)]
public sealed class WorkOrdersWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddWorkflowActivitySchema<CreateTicketWorkOrderTaskSchema>();
}
