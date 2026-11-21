using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Surveys.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Adds the Surveys workflow events and tasks, started after each change is saved.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<SurveyResponseStartedEvent, SurveyEventActivityDisplayDriver<SurveyResponseStartedEvent>>();
        services.AddActivity<SurveyPageCompletedEvent, SurveyEventActivityDisplayDriver<SurveyPageCompletedEvent>>();
        services.AddActivity<SurveyResponseCompletedEvent, SurveyEventActivityDisplayDriver<SurveyResponseCompletedEvent>>();
        services.AddActivity<SurveyResponseScreenedOutEvent, SurveyEventActivityDisplayDriver<SurveyResponseScreenedOutEvent>>();
        services.AddActivity<SurveyResponseAbandonedEvent, SurveyEventActivityDisplayDriver<SurveyResponseAbandonedEvent>>();
        services.AddActivity<SurveyResponseNeedsGradingEvent, SurveyEventActivityDisplayDriver<SurveyResponseNeedsGradingEvent>>();
        services.AddActivity<SurveyResponseGradedEvent, SurveyEventActivityDisplayDriver<SurveyResponseGradedEvent>>();
        services.AddActivity<SurveyResponsePassedEvent, SurveyEventActivityDisplayDriver<SurveyResponsePassedEvent>>();
        services.AddActivity<SurveyResponseFailedEvent, SurveyEventActivityDisplayDriver<SurveyResponseFailedEvent>>();
        services.AddActivity<SurveyResponseScoreChangedEvent, SurveyEventActivityDisplayDriver<SurveyResponseScoreChangedEvent>>();
        services.AddActivity<SurveyResponseMatchesConditionEvent, SurveyEventActivityDisplayDriver<SurveyResponseMatchesConditionEvent>>();
        services.AddActivity<SurveyPublishedEvent, SurveyEventActivityDisplayDriver<SurveyPublishedEvent>>();
        services.AddActivity<SurveyClosedEvent, SurveyEventActivityDisplayDriver<SurveyClosedEvent>>();
        services.AddActivity<SurveyQuotaReachedEvent, SurveyEventActivityDisplayDriver<SurveyQuotaReachedEvent>>();
        services.AddActivity<CreateSurveyLinkTask, SurveyTaskDisplayDriver<CreateSurveyLinkTask>>();
        services.AddActivity<CloseSurveyTask, SurveyTaskDisplayDriver<CloseSurveyTask>>();
        services.AddActivity<OpenSurveyTask, SurveyTaskDisplayDriver<OpenSurveyTask>>();
        services.AddActivity<VoidSurveyResponseTask, SurveyTaskDisplayDriver<VoidSurveyResponseTask>>();
        services.AddActivity<RegradeSurveyResponseTask, SurveyTaskDisplayDriver<RegradeSurveyResponseTask>>();
        services.AddActivity<GrantSurveyOverrideTask, SurveyTaskDisplayDriver<GrantSurveyOverrideTask>>();
        services.AddActivity<SetSurveyResponsePropertyTask, SurveyTaskDisplayDriver<SetSurveyResponsePropertyTask>>();

        services.TryAddScoped<SurveyWorkflowInputBuilder>();
        services.TryAddScoped<ISurveyWorkflowTrigger, DeferredSurveyWorkflowTrigger>();
        services.TryAddScoped<SurveyQuotaWorkflowEvents>();
        services.AddSurveyResponseObserver<SurveysWorkflowObserver>();
        services.AddSurveyObserver<SurveysWorkflowObserver>();
    }
}

/// <summary>
/// Adds the Send survey invitation task when the Survey Invitations feature is on.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class InvitationWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddActivity<SendSurveyInvitationTask, SurveyTaskDisplayDriver<SendSurveyInvitationTask>>();
}

/// <summary>
/// Describes the Surveys workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<SurveyResponseStartedEventSchema>()
            .AddWorkflowActivitySchema<SurveyPageCompletedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseCompletedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseScreenedOutEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseAbandonedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseNeedsGradingEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseGradedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponsePassedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseFailedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseScoreChangedEventSchema>()
            .AddWorkflowActivitySchema<SurveyResponseMatchesConditionEventSchema>()
            .AddWorkflowActivitySchema<SurveyPublishedEventSchema>()
            .AddWorkflowActivitySchema<SurveyClosedEventSchema>()
            .AddWorkflowActivitySchema<SurveyQuotaReachedEventSchema>()
            .AddWorkflowActivitySchema<CreateSurveyLinkTaskSchema>()
            .AddWorkflowActivitySchema<SendSurveyInvitationTaskSchema>()
            .AddWorkflowActivitySchema<CloseSurveyTaskSchema>()
            .AddWorkflowActivitySchema<OpenSurveyTaskSchema>()
            .AddWorkflowActivitySchema<VoidSurveyResponseTaskSchema>()
            .AddWorkflowActivitySchema<RegradeSurveyResponseTaskSchema>()
            .AddWorkflowActivitySchema<GrantSurveyOverrideTaskSchema>()
            .AddWorkflowActivitySchema<SetSurveyResponsePropertyTaskSchema>();
    }
}
