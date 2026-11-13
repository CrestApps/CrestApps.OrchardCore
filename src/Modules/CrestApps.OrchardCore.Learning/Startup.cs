using CrestApps.OrchardCore.Learning.Controllers;
using CrestApps.OrchardCore.Learning.Core;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Deployments;
using CrestApps.OrchardCore.Learning.Drivers;
using CrestApps.OrchardCore.Learning.Handlers;
using CrestApps.OrchardCore.Learning.Migrations;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Navigation;
using CrestApps.OrchardCore.Learning.Recipes;
using CrestApps.OrchardCore.Learning.Reports;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Registers courses, course items, enrollments, progress, the content access handler and the admin pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(LearningConstants.CollectionName));
        services.AddDataMigration<LearningMigrations>()
            .AddIndexProvider<EnrollmentIndexProvider>()
            .AddIndexProvider<EnrollmentProgressIndexProvider>()
            .AddIndexProvider<CourseStaffIndexProvider>();

        services.AddNumberSequence();
        services.AddLearningCore();
        services.AddScoped<CourseEditorOptions>();
        services.AddScoped<EnrollmentListService>();
        services.AddScoped<LearnerUserLookup>();

        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CoursePartDisplayDriver>();
        services.AddContentPart<CourseOutlinePart>()
            .UseDisplayDriver<CourseOutlinePartDisplayDriver>();
        services.AddContentPart<CourseItemPart>()
            .UseDisplayDriver<CourseItemPartDisplayDriver>()
            .AddHandler<CourseItemPartHandler>();
        services.AddContentPart<AcknowledgementPart>()
            .UseDisplayDriver<AcknowledgementPartDisplayDriver>();
        services.AddContentPart<VideoLessonPart>()
            .UseDisplayDriver<VideoLessonPartDisplayDriver>();

        services.AddDisplayDriver<Enrollment, EnrollmentRowDisplayDriver>();
        services.AddPermissionProvider<LearningPermissionsProvider>();
        services.AddNavigationProvider<LearningAdminMenu>();
        services.AddSingleton<IBackgroundTask, LearningDailyBackgroundTask>();
    }
}

/// <summary>
/// The pages a learner uses: My learning, the player and the transcript.
/// </summary>
[Feature(LearningConstants.Features.LearnerPages)]
public sealed class LearnerPagesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LearnerPageService>();
        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CourseLearnerDisplayDriver>();
        services.AddContentPart<VideoLessonPart>()
            .UseDisplayDriver<VideoLessonPlayerDisplayDriver>();
    }
}

/// <summary>
/// Lets a learner enroll on the course page.
/// </summary>
[Feature(LearningConstants.Features.SelfEnrollment)]
public sealed class SelfEnrollmentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SelfEnrollmentMarker>();
    }
}

/// <summary>
/// Starts workflows when learners enroll, progress and complete, and lets workflows change enrollments.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EnrollmentRequestedEvent, LearningEventActivityDisplayDriver<EnrollmentRequestedEvent>>();
        services.AddActivity<LearnerEnrolledEvent, LearningEventActivityDisplayDriver<LearnerEnrolledEvent>>();
        services.AddActivity<EnrollmentStatusChangedEvent, LearningEventActivityDisplayDriver<EnrollmentStatusChangedEvent>>();
        services.AddActivity<CourseStartedEvent, LearningEventActivityDisplayDriver<CourseStartedEvent>>();
        services.AddActivity<CourseItemCompletedEvent, LearningEventActivityDisplayDriver<CourseItemCompletedEvent>>();
        services.AddActivity<CourseCompletedEvent, LearningEventActivityDisplayDriver<CourseCompletedEvent>>();
        services.AddActivity<AccessExpiringEvent, LearningEventActivityDisplayDriver<AccessExpiringEvent>>();
        services.AddActivity<LearnerInactiveEvent, LearningEventActivityDisplayDriver<LearnerInactiveEvent>>();
        services.AddActivity<EnrollUserTask, LearningTaskDisplayDriver<EnrollUserTask>>();
        services.AddActivity<ApproveEnrollmentTask, LearningTaskDisplayDriver<ApproveEnrollmentTask>>();
        services.AddActivity<RejectEnrollmentTask, LearningTaskDisplayDriver<RejectEnrollmentTask>>();
        services.AddActivity<ChangeEnrollmentStatusTask, LearningTaskDisplayDriver<ChangeEnrollmentStatusTask>>();
        services.AddActivity<ExtendEnrollmentTask, LearningTaskDisplayDriver<ExtendEnrollmentTask>>();
        services.AddActivity<MarkCourseItemCompleteTask, LearningTaskDisplayDriver<MarkCourseItemCompleteTask>>();
        services.AddEnrollmentObserver<LearningWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Learning workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<EnrollmentRequestedEventSchema>()
            .AddWorkflowActivitySchema<LearnerEnrolledEventSchema>()
            .AddWorkflowActivitySchema<EnrollmentStatusChangedEventSchema>()
            .AddWorkflowActivitySchema<CourseStartedEventSchema>()
            .AddWorkflowActivitySchema<CourseItemCompletedEventSchema>()
            .AddWorkflowActivitySchema<CourseCompletedEventSchema>()
            .AddWorkflowActivitySchema<AccessExpiringEventSchema>()
            .AddWorkflowActivitySchema<LearnerInactiveEventSchema>()
            .AddWorkflowActivitySchema<EnrollUserTaskSchema>()
            .AddWorkflowActivitySchema<ApproveEnrollmentTaskSchema>()
            .AddWorkflowActivitySchema<RejectEnrollmentTaskSchema>()
            .AddWorkflowActivitySchema<ChangeEnrollmentStatusTaskSchema>()
            .AddWorkflowActivitySchema<ExtendEnrollmentTaskSchema>()
            .AddWorkflowActivitySchema<MarkCourseItemCompleteTaskSchema>();
    }
}

/// <summary>
/// Imports the Learning enrollments recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LearningEnrollmentsRecipeStep>();
    }
}

/// <summary>
/// Exports enrollments with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LearningEnrollmentsDeploymentSource, LearningEnrollmentsDeploymentStep, LearningEnrollmentsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Learning recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningEnrollmentsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningEnrollmentsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Learning data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<LearningReportDataSource>();
        services.AddScoped<ILearningReportDataSet, LearningEnrollmentsReportDataSet>();
        services.AddScoped<ILearningReportDataSet, LearningItemProgressReportDataSet>();
        services.AddScoped<ILearningReportDataSet, LearningCompletionsReportDataSet>();
        services.AddScoped<ILearningReportDataSet, LearningCourseStaffReportDataSet>();
    }
}
