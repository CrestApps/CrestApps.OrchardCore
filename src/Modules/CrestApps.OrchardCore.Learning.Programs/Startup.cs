using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Core.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Drivers;
using CrestApps.OrchardCore.Learning.Programs.Migrations;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Navigation;
using CrestApps.OrchardCore.Learning.Programs.Recipes;
using CrestApps.OrchardCore.Learning.Programs.Reports;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using CrestApps.OrchardCore.Learning.Programs.Workflows;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Notifications;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Programs;

/// <summary>
/// Registers learning paths: the content type and its part, the people on each path and the learner's paths.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ProgramsConstants.CollectionName));
        services.AddDataMigration<ProgramsMigrations>()
            .AddIndexProvider<LearningPathProgressIndexProvider>()
            .AddIndexProvider<LearningRequirementIndexProvider>()
            .AddIndexProvider<RequiredLearningAssignmentIndexProvider>();

        services.AddLearningPathsCore();
        services.AddContentPart<LearningPathPart>()
            .UseDisplayDriver<LearningPathPartDisplayDriver>();
        services.AddDisplayDriver<LearningPathProgress, LearningPathProgressRowDisplayDriver>();
        services.AddPersonalDataContributor<ProgramsPersonalDataContributor>();
        services.AddNavigationProvider<RequiredLearningAccountMenu>();
    }
}

/// <summary>
/// Registers required learning: its pages, the hourly upkeep and the evidence of exemptions.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
public sealed class RequiredLearningStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRequiredLearningCore();
        services.AddPermissionProvider<ProgramsPermissionProvider>();
        services.AddNavigationProvider<ProgramsAdminMenu>();
        services.AddDisplayDriver<LearningRequirement, LearningRequirementRowDisplayDriver>();
        services.AddDisplayDriver<RequiredLearningAssignment, RequiredLearningAssignmentRowDisplayDriver>();
        services.AddDisplayDriver<RequiredLearningPreviewRow, RequiredLearningPreviewRowDisplayDriver>();
        services.AddProtectedFileArea<RequiredLearningEvidence>(RequiredLearningEvidence.Folder);
        services.AddScoped<IProtectedFileAccessPolicy<RequiredLearningEvidence>, RequiredLearningEvidencePolicy>();
        services.AddSingleton<IBackgroundTask, RequiredLearningBackgroundTask>();
    }
}

/// <summary>
/// Lets required learning require a certification path.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(CertificationsConstants.Features.Certifications)]
public sealed class CertificationTargetStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRequiredLearningCertificationTarget();
    }
}

/// <summary>
/// Awards the education credits of a completed learning path.
/// </summary>
[Feature(ProgramsConstants.Features.Paths)]
[RequireFeatures(CertificationsConstants.Features.Certifications)]
public sealed class PathCreditsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningPathObserver<LearningPathCreditsObserver>();
    }
}

/// <summary>
/// Tells learners and managers about required learning and completed paths.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(ProgramsConstants.NotificationTypes.Required, LocalizationSource.Create<NotificationsStartup>("Learning required"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a course or a learning path is required of you."));
        services.AddNotificationType(ProgramsConstants.NotificationTypes.DueSoon, LocalizationSource.Create<NotificationsStartup>("Required learning due soon"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("Before learning required of you is due."));
        services.AddNotificationType(ProgramsConstants.NotificationTypes.Overdue, LocalizationSource.Create<NotificationsStartup>("Required learning overdue"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When learning required of you, or of someone who reports to you, is overdue."));
        services.AddNotificationType(ProgramsConstants.NotificationTypes.PathCompleted, LocalizationSource.Create<NotificationsStartup>("Learning path completed"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When you complete a learning path."));
        services.AddRequiredLearningObserver<ProgramsNotificationObserver>();
        services.AddLearningPathObserver<ProgramsNotificationObserver>();
    }
}

/// <summary>
/// Shows managers the required learning of their team.
/// </summary>
[Feature(ProgramsConstants.Features.TeamLearning)]
public sealed class TeamLearningStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<TeamLearningAdminMenu>();
        services.AddDisplayDriver<TeamLearningRow, TeamLearningRowDisplayDriver>();
    }
}

/// <summary>
/// Adds the Learning Path Completed workflow event and triggers it.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class PathsWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningPathObserver<ProgramsWorkflowObserver>();
        services.AddActivity<LearningPathCompletedEvent, LearningPathCompletedEventDisplayDriver>();
    }
}

/// <summary>
/// Adds the workflow events and tasks of required learning, and triggers the events.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class RequiredLearningWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRequiredLearningObserver<ProgramsWorkflowObserver>();
        services.AddActivity<LearningRequiredEvent, RequiredLearningEventDisplayDriver<LearningRequiredEvent>>();
        services.AddActivity<RequiredLearningDueSoonEvent, RequiredLearningEventDisplayDriver<RequiredLearningDueSoonEvent>>();
        services.AddActivity<RequiredLearningOverdueEvent, RequiredLearningEventDisplayDriver<RequiredLearningOverdueEvent>>();
        services.AddActivity<RequiredLearningExemptionGrantedEvent, RequiredLearningEventDisplayDriver<RequiredLearningExemptionGrantedEvent>>();
        services.AddActivity<RequireLearningTask, RequireLearningTaskDisplayDriver>();
        services.AddActivity<GrantRequiredLearningExemptionTask, GrantRequiredLearningExemptionTaskDisplayDriver>();
    }
}

/// <summary>
/// Imports required learning rules with a recipe step.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RequiredLearningRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LearningRequirementsRecipeStep>();
    }
}

/// <summary>
/// Exports required learning rules with a deployment step.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class RequiredLearningDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LearningRequirementsDeploymentSource, LearningRequirementsDeploymentStep, LearningRequirementsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the required learning recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RequiredLearningRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningRequirementsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningRequirementsDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the workflow activities of required learning to CrestApps Recipes.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class RequiredLearningWorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<LearningRequiredEventSchema>()
            .AddWorkflowActivitySchema<RequiredLearningDueSoonEventSchema>()
            .AddWorkflowActivitySchema<RequiredLearningOverdueEventSchema>()
            .AddWorkflowActivitySchema<RequiredLearningExemptionGrantedEventSchema>()
            .AddWorkflowActivitySchema<RequireLearningTaskSchema>()
            .AddWorkflowActivitySchema<GrantRequiredLearningExemptionTaskSchema>();
    }
}

/// <summary>
/// Describes the learning path workflow event to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class PathsWorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<LearningPathCompletedEventSchema>();
    }
}

/// <summary>
/// Adds the Learning programs data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LearningPathProgressReportDataSet>();
        services.AddDataSource<LearningProgramsDataSource>();
    }
}

/// <summary>
/// Adds the required learning data sets to the Learning programs data source.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class RequiredLearningDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LearningRequirementReportDataSet>();
        services.AddScoped<RequiredLearningStateReportDataSet>();
    }
}
