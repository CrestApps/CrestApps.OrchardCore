using CrestApps.OrchardCore.Calendars.Deployments;
using CrestApps.OrchardCore.Calendars.Recipes;
using CrestApps.OrchardCore.Calendars.Recipes.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Describes the Calendars recipe and deployment steps to CrestApps Recipes, so they can be checked and written with help.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CalendarsRecipeStepSchema>();
        services.AddDeploymentStepSchema<CalendarsDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the part of the Calendar dashboard widget to CrestApps Recipes when the admin dashboard is on.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.AdminDashboard)]
public sealed class DashboardRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, CalendarWidgetPartSchemaDefinition>();
    }
}

/// <summary>
/// Describes the calendar workflow events and tasks to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CalendarEventCreatedEventSchema>()
            .AddWorkflowActivitySchema<CalendarEventUpdatedEventSchema>()
            .AddWorkflowActivitySchema<CalendarEventCancelledEventSchema>()
            .AddWorkflowActivitySchema<CalendarEventDeletedEventSchema>()
            .AddWorkflowActivitySchema<CalendarAttendeeInvitedEventSchema>()
            .AddWorkflowActivitySchema<CalendarAttendeeRespondedEventSchema>()
            .AddWorkflowActivitySchema<CreateCalendarEventTaskSchema>()
            .AddWorkflowActivitySchema<UpdateCalendarEventTaskSchema>()
            .AddWorkflowActivitySchema<CancelCalendarEventTaskSchema>()
            .AddWorkflowActivitySchema<GetCalendarEventTaskSchema>()
            .AddWorkflowActivitySchema<FindCalendarEventsTaskSchema>()
            .AddWorkflowActivitySchema<AddCalendarAttendeeTaskSchema>()
            .AddWorkflowActivitySchema<RemoveCalendarAttendeeTaskSchema>()
            .AddWorkflowActivitySchema<FindFreeTimeTaskSchema>();
    }
}

/// <summary>
/// Describes the workflow events raised by calendar reminders to CrestApps Recipes.
/// </summary>
[Feature(CalendarsConstants.Features.Reminders)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRemindersRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CalendarEventStartingSoonEventSchema>()
            .AddWorkflowActivitySchema<CalendarEventCompletedEventSchema>();
    }
}
