using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Scheduling.Deployments;
using CrestApps.OrchardCore.Scheduling.Recipes;
using CrestApps.OrchardCore.Scheduling.Recipes.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Imports the Scheduling recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<SchedulingRecipeStep>();
    }
}

/// <summary>
/// Exports the scheduling setup with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SchedulingDeploymentSource, SchedulingDeploymentStep, SchedulingDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Scheduling recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CrestApps.OrchardCore.Recipes.Core.IRecipeStep, SchedulingRecipeStepSchema>();
        services.AddDeploymentStepSchema<SchedulingDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the appointment workflow events and tasks to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AppointmentRequestedEventSchema>()
            .AddWorkflowActivitySchema<AppointmentConfirmedEventSchema>()
            .AddWorkflowActivitySchema<AppointmentRescheduledEventSchema>()
            .AddWorkflowActivitySchema<AppointmentCancelledEventSchema>()
            .AddWorkflowActivitySchema<AppointmentCompletedEventSchema>()
            .AddWorkflowActivitySchema<AppointmentNoShowEventSchema>()
            .AddWorkflowActivitySchema<AppointmentHoldExpiredEventSchema>()
            .AddWorkflowActivitySchema<WaitlistSlotAvailableEventSchema>()
            .AddWorkflowActivitySchema<FindAppointmentSlotsTaskSchema>()
            .AddWorkflowActivitySchema<BookAppointmentTaskSchema>()
            .AddWorkflowActivitySchema<ConfirmAppointmentTaskSchema>()
            .AddWorkflowActivitySchema<RescheduleAppointmentTaskSchema>()
            .AddWorkflowActivitySchema<CancelAppointmentTaskSchema>()
            .AddWorkflowActivitySchema<GetAppointmentTaskSchema>()
            .AddWorkflowActivitySchema<AddToWaitlistTaskSchema>();
    }
}

/// <summary>
/// Describes the appointment reminder workflow event to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows, CalendarsConstants.Features.Reminders)]
public sealed class WorkflowRemindersRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<AppointmentStartingSoonEventSchema>();
    }
}
