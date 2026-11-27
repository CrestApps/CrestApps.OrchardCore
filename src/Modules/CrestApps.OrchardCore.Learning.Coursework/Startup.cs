using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Drivers;
using CrestApps.OrchardCore.Learning.Coursework.Migrations;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Recipes;
using CrestApps.OrchardCore.Learning.Coursework.Reports;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using CrestApps.OrchardCore.Learning.Coursework.Workflows;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Coursework;

/// <summary>
/// Registers Learning coursework: the coursework course item and the rubric with their parts, the coursework panel of
/// the learner player, the submissions and grading pages, the submissions in the grading list, and personal data.
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
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CourseworkConstants.CollectionName));
        services.Configure<CourseworkOptions>(_configuration.GetSection("CrestApps:Learning:Coursework"));
        services.AddDataMigration<CourseworkMigrations>()
            .AddIndexProvider<SubmissionIndexProvider>();

        services.AddLearningCourseworkCore();
        services.AddContentPart<CourseworkPart>()
            .UseDisplayDriver<CourseworkPartDisplayDriver>()
            .UseDisplayDriver<CourseworkPartLearnerDisplayDriver>();
        services.AddContentPart<RubricPart>()
            .UseDisplayDriver<RubricPartDisplayDriver>();

        services.AddScoped<CourseworkLearnerResolver>();
        services.AddDisplayDriver<SubmissionRow, SubmissionRowDisplayDriver>();
        services.AddDisplayDriver<SubmissionGradingViewModel, SubmissionGradingDisplayDriver>();
        services.AddPersonalDataContributor<CourseworkPersonalDataContributor>();
    }
}

/// <summary>
/// Tells learners when their coursework is graded or returned, through the Notifications module.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(CourseworkConstants.NotificationTypes.Graded, LocalizationSource.Create<NotificationsStartup>("Coursework graded"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When coursework you submitted is graded, or graded again."));
        services.AddNotificationType(CourseworkConstants.NotificationTypes.Returned, LocalizationSource.Create<NotificationsStartup>("Coursework returned"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When coursework you submitted is returned for you to submit again."));
        services.AddSubmissionObserver<CourseworkNotificationObserver>();
    }
}

/// <summary>
/// Adds the Coursework Submitted and Coursework Graded workflow events and the Return Coursework For Resubmission task.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CourseworkSubmittedEvent, CourseworkEventDisplayDriver<CourseworkSubmittedEvent>>();
        services.AddActivity<CourseworkGradedEvent, CourseworkEventDisplayDriver<CourseworkGradedEvent>>();
        services.AddActivity<ReturnCourseworkTask, ReturnCourseworkTaskDisplayDriver>();
        services.AddSubmissionObserver<CourseworkWorkflowObserver>();
    }
}

/// <summary>
/// Describes the coursework workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CourseworkSubmittedEventSchema>()
            .AddWorkflowActivitySchema<CourseworkGradedEventSchema>()
            .AddWorkflowActivitySchema<ReturnCourseworkTaskSchema>();
    }
}

/// <summary>
/// Adds the Learning coursework data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SubmissionReportDataSet>();
        services.AddDataSource<LearningCourseworkDataSource>();
    }
}

/// <summary>
/// Imports coursework submissions with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddRecipeExecutionStep<LearningSubmissionsRecipeStep>();
    }
}

/// <summary>
/// Exports coursework submissions with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDeployment<LearningSubmissionsDeploymentSource, LearningSubmissionsDeploymentStep, LearningSubmissionsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the submissions recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningSubmissionsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningSubmissionsDeploymentStepSchema>();
    }
}
