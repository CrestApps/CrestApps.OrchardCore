using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.PersonalData.Core;
using OrchardCore.Notifications;
using OrchardCore.Localization;
using OrchardCore.AuditTrail.Services.Models;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Learning.Assessments.Webhooks;
using CrestApps.OrchardCore.Learning.Assessments.Notifications;
using CrestApps.OrchardCore.Learning.Assessments.AuditTrail;
using OrchardCore.Workflows.Helpers;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Learning.Assessments.Workflows;
using OrchardCore.Recipes;
using OrchardCore.Deployment;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Recipes;
using CrestApps.OrchardCore.Learning.Assessments.Deployments;
using OrchardCore.DataSources;
using CrestApps.OrchardCore.Learning.Assessments.Reports;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Drivers;
using CrestApps.OrchardCore.Learning.Assessments.Handlers;
using CrestApps.OrchardCore.Learning.Assessments.Migrations;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Navigation;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Registers questions, banks, the quiz item, attempts, their deadlines and the grading pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AttemptDeadlineWork.CollectionName));
        services.AddDataMigration<AssessmentsMigrations>()
            .AddIndexProvider<AssessmentAttemptIndexProvider>()
            .AddIndexProvider<AssessmentOverrideIndexProvider>()
            .AddIndexProvider<GradeScaleIndexProvider>()
            .AddIndexProvider<QuestionSourceIndexProvider>();

        services.AddLearningAssessmentsCore();

        // The pictures only imported QTI feedback shows, kept out of the media library (review round 9).
        services.AddProtectedFileArea<CrestApps.OrchardCore.Learning.Assessments.Core.Qti.QtiFeedbackImages>(CrestApps.OrchardCore.Learning.Assessments.Core.Qti.QtiFeedbackImages.Folder);

        services.AddContentPart<QuestionPart>()
            .UseDisplayDriver<QuestionPartDisplayDriver>();
        services.AddContentPart<ChoiceQuestionPart>()
            .UseDisplayDriver<ChoiceQuestionPartDisplayDriver>();
        services.AddContentPart<TrueFalseQuestionPart>()
            .UseDisplayDriver<TrueFalseQuestionPartDisplayDriver>();
        services.AddContentPart<ShortTextQuestionPart>()
            .UseDisplayDriver<ShortTextQuestionPartDisplayDriver>();
        services.AddContentPart<NumericQuestionPart>()
            .UseDisplayDriver<NumericQuestionPartDisplayDriver>();
        services.AddContentPart<MatchingQuestionPart>()
            .UseDisplayDriver<MatchingQuestionPartDisplayDriver>();
        services.AddContentPart<OrderingQuestionPart>()
            .UseDisplayDriver<OrderingQuestionPartDisplayDriver>();
        services.AddContentPart<FillInBlanksQuestionPart>()
            .UseDisplayDriver<FillInBlanksQuestionPartDisplayDriver>();
        services.AddContentPart<EssayQuestionPart>()
            .UseDisplayDriver<EssayQuestionPartDisplayDriver>();
        services.AddContentPart<QuizPart>()
            .UseDisplayDriver<QuizPartDisplayDriver>()
            .UseDisplayDriver<QuizPartLearnerDisplayDriver>()
            .AddHandler<QuizCloseDateHandler>();

        services.AddDisplayDriver<GradingQueueRow, GradingQueueRowDisplayDriver>();
        services.AddPermissionProvider<AssessmentsPermissionsProvider>();
        services.AddNavigationProvider<AssessmentsAdminMenu>();
        services.AddSingleton<IBackgroundTask, AttemptDeadlineBackgroundTask>();
        services.AddPersonalDataContributor<AssessmentsPersonalDataContributor>();
    }
}

/// <summary>
/// Adds the Learning assessments data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class AssessmentsDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<AssessmentsReportDataSource>();
        services.AddScoped<IAssessmentsReportDataSet, LearningAttemptsReportDataSet>();
        services.AddScoped<IAssessmentsReportDataSet, LearningAttemptAnswersReportDataSet>();
        services.AddScoped<IAssessmentsReportDataSet, LearningAttemptScoreChangesReportDataSet>();
        services.AddScoped<IAssessmentsReportDataSet, LearningQuizOverridesReportDataSet>();
    }
}

/// <summary>
/// Adds the quiz results and item analysis report.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class AssessmentsReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<QuizItemAnalysisReport>();
    }
}

/// <summary>
/// Imports grade scales and quiz attempts with recipe steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class AssessmentsRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddRecipeExecutionStep<LearningGradeScalesRecipeStep>();
        services.AddRecipeExecutionStep<LearningAttemptsRecipeStep>();
    }
}

/// <summary>
/// Exports grade scales and quiz attempts with deployment steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class AssessmentsDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDeployment<LearningGradeScalesDeploymentSource, LearningGradeScalesDeploymentStep, LearningGradeScalesDeploymentStepDisplayDriver>();
        services.AddDeployment<LearningAttemptsDeploymentSource, LearningAttemptsDeploymentStep, LearningAttemptsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Learning assessments recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AssessmentsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningGradeScalesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, LearningAttemptsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningGradeScalesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<LearningAttemptsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the quiz attempt workflow events and tasks.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class AssessmentsWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AttemptStartedEvent, AttemptEventActivityDisplayDriver<AttemptStartedEvent>>();
        services.AddActivity<AttemptSubmittedEvent, AttemptEventActivityDisplayDriver<AttemptSubmittedEvent>>();
        services.AddActivity<AttemptNeedsGradingEvent, AttemptEventActivityDisplayDriver<AttemptNeedsGradingEvent>>();
        services.AddActivity<AttemptGradedEvent, AttemptEventActivityDisplayDriver<AttemptGradedEvent>>();
        services.AddActivity<AttemptScoreChangedEvent, AttemptEventActivityDisplayDriver<AttemptScoreChangedEvent>>();
        services.AddActivity<VoidAttemptTask, AttemptTaskDisplayDriver<VoidAttemptTask>>();
        services.AddActivity<RegradeAttemptTask, AttemptTaskDisplayDriver<RegradeAttemptTask>>();
        services.AddAttemptObserver<AssessmentsWorkflowObserver>();
    }
}

/// <summary>
/// Describes the quiz attempt workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class AssessmentsWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AttemptStartedEventSchema>()
            .AddWorkflowActivitySchema<AttemptSubmittedEventSchema>()
            .AddWorkflowActivitySchema<AttemptNeedsGradingEventSchema>()
            .AddWorkflowActivitySchema<AttemptGradedEventSchema>()
            .AddWorkflowActivitySchema<AttemptScoreChangedEventSchema>()
            .AddWorkflowActivitySchema<VoidAttemptTaskSchema>()
            .AddWorkflowActivitySchema<RegradeAttemptTaskSchema>();
    }
}

/// <summary>
/// Tells learners when a quiz attempt is graded.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class AssessmentsNotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(AssessmentsConstants.NotificationTypes.Graded, LocalizationSource.Create<AssessmentsNotificationsStartup>("Quiz graded"), type => type.Description = LocalizationSource.Create<AssessmentsNotificationsStartup>("When a person has graded a quiz attempt of yours."));
        services.AddAttemptObserver<AssessmentsNotificationObserver>();
    }
}

/// <summary>
/// Records quiz grading in the Audit Trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AssessmentsAuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, AssessmentsAuditTrailEvents>();
        services.AddAttemptObserver<AssessmentsAuditTrailObserver>();
    }
}

/// <summary>
/// Publishes quiz attempt webhook events.
/// </summary>
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class AssessmentsWebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent(AssessmentsWebhookObserver.AttemptSubmitted, "Quiz attempt submitted", "Learning");
        services.AddWebhookEvent(AssessmentsWebhookObserver.AttemptGraded, "Quiz attempt graded", "Learning");
        services.AddAttemptObserver<AssessmentsWebhookObserver>();
    }
}

/// <summary>
/// Lets questions be written in a spreadsheet and imported, or exported, through ContentTransfer.
/// </summary>
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class AssessmentsContentTransferStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPartImportHandler<QuestionPart, QuestionPartImportHandler>();
        services.AddContentPartImportHandler<ChoiceQuestionPart, ChoiceQuestionPartImportHandler>();
        services.AddContentPartImportHandler<TrueFalseQuestionPart, TrueFalseQuestionPartImportHandler>();
        services.AddContentPartImportHandler<ShortTextQuestionPart, ShortTextQuestionPartImportHandler>();
        services.AddContentPartImportHandler<NumericQuestionPart, NumericQuestionPartImportHandler>();
        services.AddContentPartImportHandler<EssayQuestionPart, EssayQuestionPartImportHandler>();
        services.AddContentPartImportHandler<MatchingQuestionPart, MatchingQuestionPartImportHandler>();
        services.AddContentPartImportHandler<OrderingQuestionPart, OrderingQuestionPartImportHandler>();
        services.AddContentPartImportHandler<FillInBlanksQuestionPart, FillInBlanksQuestionPartImportHandler>();

        // One import creates questions of every kind, each row naming its type.
        services.AddSingleton<IContentImportRowTypeProvider, QuestionRowTypeProvider>();

        // The question bank of an import, and a row matched to the question it imported before.
        services.AddScoped<IDisplayDriver<ImportContent>, QuestionImportOptionsDisplayDriver>();
        services.AddScoped<IContentImportHandler, QuestionBankImportHandler>();
        services.AddScoped<IContentImportRowFilter, QuestionSourceRowFilter>();
    }
}

/// <summary>
/// Reads QTI 3.0 and QTI 2.1 packages and item files as questions through ContentTransfer.
/// </summary>
[Feature(AssessmentsConstants.Features.QtiImport)]
public sealed class QtiImportStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IContentTransferFileFormatProvider, QtiPackageFileFormatProvider>();
        services.AddSingleton<IContentTransferFileFormatProvider, QtiItemFileFormatProvider>();
        services.AddScoped<IContentImportHandler, QtiQuestionImportHandler>();
        services.AddScoped<IContentImportHandler, QtiMediaImportHandler>();
    }
}
