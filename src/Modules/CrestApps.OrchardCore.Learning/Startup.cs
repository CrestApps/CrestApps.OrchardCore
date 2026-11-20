using CrestApps.OrchardCore.Learning.StructuredData;
using CrestApps.OrchardCore.Seo;
using CrestApps.OrchardCore.Learning.AuditTrail;
using CrestApps.OrchardCore.Learning.Controllers;
using CrestApps.OrchardCore.Learning.Core;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Deployments;
using CrestApps.OrchardCore.Learning.Drivers;
using CrestApps.OrchardCore.Learning.Handlers;
using CrestApps.OrchardCore.Learning.Migrations;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Navigation;
using CrestApps.OrchardCore.Learning.Notifications;
using CrestApps.OrchardCore.Learning.Pipelines;
using CrestApps.OrchardCore.Learning.Recipes;
using CrestApps.OrchardCore.Learning.Reports;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Shapes;
using CrestApps.OrchardCore.Learning.Webhooks;
using CrestApps.OrchardCore.Learning.Workflows;
using CrestApps.OrchardCore.PageSections;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// Registers courses, course items, enrollments, progress, the content access handler and the admin pages.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">The tenant configuration.</param>
    public Startup(IShellConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(LearningConstants.CollectionName));
        services.AddDataMigration<LearningMigrations>()
            .AddIndexProvider<EnrollmentIndexProvider>()
            .AddIndexProvider<EnrollmentProgressIndexProvider>()
            .AddIndexProvider<CourseStaffIndexProvider>()
            .AddIndexProvider<LearningSecretIndexProvider>()
            .AddIndexProvider<LearningSourceIndexProvider>();

        services.AddNumberSequence();
        services.AddLearningCore();
        services.Configure<LearningUpkeepOptions>(_configuration.GetSection("CrestApps:Learning:Upkeep"));
        services.AddScoped<CourseEditorOptions>();
        services.AddUserAudienceRuleSource<CourseLearnerAudienceRuleSource>();
        services.AddScoped<EnrollmentListService>();
        services.AddScoped<LearningRefusalText>();
        services.AddScoped<LearnerUserLookup>();
        services.AddPersonalDataContributor<LearningPersonalDataContributor>();
        services.AddScoped<LearnerRecordSummaryService>();
        services.AddDisplayDriver<User, UserLearningDisplayDriver>();

        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CoursePartDisplayDriver>()
            .AddHandler<CourseSettingsGuardHandler>();
        services.AddContentPart<CourseOutlinePart>()
            .UseDisplayDriver<CourseOutlinePartDisplayDriver>();
        services.AddContentPart<CourseItemPart>()
            .UseDisplayDriver<CourseItemPartDisplayDriver>()
            .AddHandler<CourseItemPartHandler>();
        services.AddScoped<IContentHandler, CourseFeedHandler>();
        services.AddShapeTableProvider<CourseItemSummaryShapes>();
        services.AddShapeTableProvider<CourseItemFileShapes>();
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
        services.AddScoped<CourseItemConditionText>();
        services.AddScoped<LearningTimeText>();
        services.AddScoped<CourseCatalogService>();
        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CourseLearnerDisplayDriver>();
        services.AddContentPart<VideoLessonPart>()
            .UseDisplayDriver<VideoLessonPlayerDisplayDriver>();
        services.AddDisplayDriver<UserMenu, LearningUserMenuDisplayDriver>();
        services.AddNavigationProvider<LearningAccountMenu>();
    }
}

/// <summary>
/// Course categories: the category field and taxonomy of courses, and the taxonomy filter of the admin list of courses.
/// </summary>
[Feature(LearningConstants.Features.CourseCategories)]
public sealed class CourseCategoriesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CourseCategoriesMigrations>();
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
        services.AddDataPipelineStepType<EnrollLearnersStep>();
        services.AddDisplayDriver<DataPipelineStep, EnrollLearnersStepDisplayDriver>();
    }
}

/// <summary>
/// Tells learners they are enrolled, that a request was declined, and that a course is due soon or overdue, tells
/// instructors a completion needs review, and tells approvers someone asked to enroll, through Orchard Core notifications.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(LearningConstants.NotificationTypes.Enrolled, LocalizationSource.Create<NotificationsStartup>("Enrolled in a course"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When you are enrolled in a course."));
        services.AddNotificationType(LearningConstants.NotificationTypes.EnrollmentDeclined, LocalizationSource.Create<NotificationsStartup>("Enrollment request declined"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When your request to enroll in a course that needs approval is declined."));
        services.AddNotificationType(LearningConstants.NotificationTypes.DueSoon, LocalizationSource.Create<NotificationsStartup>("Course due soon"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("A few days before a course you are taking is due."));
        services.AddNotificationType(LearningConstants.NotificationTypes.ReviewNeeded, LocalizationSource.Create<NotificationsStartup>("Completion needs review"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a result a completed enrollment in a course you teach rests on changes after completion."));
        services.AddNotificationType(LearningConstants.NotificationTypes.Overdue, LocalizationSource.Create<NotificationsStartup>("Course overdue"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a course you are taking passes its due date."));
        services.AddNotificationType(LearningConstants.NotificationTypes.EnrollmentRequested, LocalizationSource.Create<NotificationsStartup>("Enrollment request to approve"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When someone asks to enroll in a course whose enrollments you may approve."));
        services.AddScoped<EnrollmentApprovers>();
        services.AddEnrollmentObserver<LearningNotificationObserver>();
    }
}

/// <summary>
/// Writes the changes people make to enrollments by hand, and exports of learner records, to the Audit Trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, LearningAuditTrailEvents>();
        services.AddScoped<LearningAuditTrailObserver>();
        services.AddScoped<IEnrollmentObserver>(sp => sp.GetRequiredService<LearningAuditTrailObserver>());
        services.AddScoped<ILearnerRecordExportObserver>(sp => sp.GetRequiredService<LearningAuditTrailObserver>());
    }
}

/// <summary>
/// Publishes the Learning webhook events.
/// </summary>
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent(LearningWebhookObserver.EnrollmentCreated, "Learner enrolled", "Learning");
        services.AddWebhookEvent(LearningWebhookObserver.CourseCompleted, "Course completed", "Learning");
        services.AddEnrollmentObserver<LearningWebhookObserver>();
    }
}

/// <summary>
/// Adds the Learning coded reports under Reports.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LearningReportText>();
        services.AddReport<LearningCourseCompletionReport>();
        services.AddReport<LearningTranscriptReport>();
        services.AddReport<LearningInactiveLearnersReport>();
        services.AddDisplayDriver<ReportFilter, LearningTranscriptFilterDisplayDriver>();
    }
}

/// <summary>
/// Adds the Instructor capability, the Learning section of the staff profile, and My learning on a staff member's page.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffCapability(LearningConstants.InstructorCapability, LocalizationSource.Create<StaffStartup>("Instructor"), LocalizationSource.Create<StaffStartup>("Teaches courses and can be named on a course's staff."), 60);
        services.AddPageSection(StaffProfileSections.PageName, StaffLearningProfileDisplayDriver.SectionName, LocalizationSource.Create<StaffStartup>("Learning"), "fa-solid fa-graduation-cap", 40);
        services.AddDisplayDriver<StaffMember, StaffLearningProfileDisplayDriver>();
    }
}

/// <summary>
/// Puts My learning on a staff member's own page when the learner pages are on.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff, LearningConstants.Features.LearnerPages)]
public sealed class StaffLearnerPagesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffHomeLinkProvider<LearningStaffHomeLinks>();
    }
}

/// <summary>
/// Describes courses with schema.org when Advanced SEO is on.
/// </summary>
[RequireFeatures(StructuredDataConstants.Feature.Area)]
public sealed class StructuredDataStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStructuredDataProvider, CourseStructuredDataProvider>();
        services.AddScoped<IStructuredDataProvider, CourseProviderStructuredDataProvider>();
        services.Configure<StructuredDataOptions>(options => options.SelfDescribingParts.Add(nameof(CoursePart)));
    }
}
