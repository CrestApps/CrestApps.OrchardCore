using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Certifications.BackgroundTasks;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Deployments;
using CrestApps.OrchardCore.Learning.Certifications.Drivers;
using CrestApps.OrchardCore.Learning.Certifications.Handlers;
using CrestApps.OrchardCore.Learning.Certifications.Migrations;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Pipelines;
using CrestApps.OrchardCore.Learning.Certifications.Recipes;
using CrestApps.OrchardCore.Learning.Certifications.Reports;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.Workflows;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataPipelines.Models;
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

namespace CrestApps.OrchardCore.Learning.Certifications;

/// <summary>
/// Registers certification paths: their card on the certification editor, the people on each path, the learner's
/// certifications page and the hourly upkeep.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options =>
        {
            options.Collections.Add(CertificationsConstants.CollectionName);
            options.Collections.Add(CertificationsConstants.RenewalWorkCollectionName);
            options.Collections.Add(CertificationsConstants.CreditLedgerCollectionName);
        });
        services.AddDataMigration<CertificationsMigrations>()
            .AddIndexProvider<CertificationPathIndexProvider>()
            .AddIndexProvider<CertificationPathProgressIndexProvider>()
            .AddIndexProvider<EducationCreditTypeIndexProvider>()
            .AddIndexProvider<ExternalLearningSubmissionIndexProvider>();

        services.AddLearningCertificationsCore();

        services.TryAddScoped<PendingCertificationPath>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Certification>, CertificationPathCatalogHandler>());
        services.AddDisplayDriver<Certification, CertificationPathDisplayDriver>();
        services.AddDisplayDriver<CertificationPathProgress, CertificationPathProgressRowDisplayDriver>();
        services.AddSingleton<IBackgroundTask, CertificationUpkeepBackgroundTask>();
        services.AddScoped<IEnrollmentReviewActionProvider, CertificationReviewActionProvider>();
        services.AddScoped<IEnrollmentReviewActionProvider, CreditsReviewActionProvider>();
        services.AddCertificationPathObserver<CertificationTelemetryObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICertificationRecordObserver, CertificationTelemetryObserver>());
        services.AddPersonalDataContributor<CertificationsPersonalDataContributor>();
        services.AddContentPart<EducationCreditsPart>()
            .UseDisplayDriver<EducationCreditsPartDisplayDriver>();
        services.AddDisplayDriver<EducationCreditType, EducationCreditTypeRowDisplayDriver>();
        services.AddDisplayDriver<ExternalLearningSubmission, ExternalLearningRowDisplayDriver>();
        services.AddPermissionProvider<CreditsPermissionProvider>();
        services.AddNavigationProvider<CreditsAdminMenu>();
        services.AddProtectedFileArea<ExternalLearningEvidence>(ExternalLearningEvidence.Folder);
        services.AddScoped<IProtectedFileAccessPolicy<ExternalLearningEvidence>, ExternalLearningEvidencePolicy>();
    }
}

/// <summary>
/// Tells holders about their certificates.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(CertificationsConstants.NotificationTypes.Issued, LocalizationSource.Create<NotificationsStartup>("Certificate issued"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a certificate you earned through learning is issued or renewed."));
        services.AddNotificationType(CertificationsConstants.NotificationTypes.Expiring, LocalizationSource.Create<NotificationsStartup>("Certificate expiring"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("Before a certificate you earned through learning expires."));
        services.AddNotificationType(CertificationsConstants.NotificationTypes.RenewalOpened, LocalizationSource.Create<NotificationsStartup>("Certificate renewal open"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When you can renew a certificate you earned through learning."));
        services.AddNotificationType(CertificationsConstants.NotificationTypes.Lapsed, LocalizationSource.Create<NotificationsStartup>("Certificate lapsed"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a certificate you earned through learning expired without a renewal."));
        services.AddScoped<CertificationNotificationObserver>();
        services.AddScoped<ICertificationRecordObserver>(provider => provider.GetRequiredService<CertificationNotificationObserver>());
        services.AddScoped<ICertificationExpiryObserver>(provider => provider.GetRequiredService<CertificationNotificationObserver>());
        services.AddScoped<ICertificationPathObserver>(provider => provider.GetRequiredService<CertificationNotificationObserver>());
    }
}

/// <summary>
/// Adds the certification compliance report.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<CertificationComplianceReport>();
    }
}

/// <summary>
/// Adds the Learning certifications data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CertificationProgressReportDataSet>();
        services.AddScoped<EducationCreditReportDataSet>();
        services.AddScoped<ExternalLearningReportDataSet>();
        services.AddDataPipelineStepType<RecordExternalLearningStep>();
        services.AddDisplayDriver<DataPipelineStep, RecordExternalLearningStepDisplayDriver>();
        services.AddDataSource<LearningCertificationsDataSource>();
    }
}

/// <summary>
/// Imports certification paths with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LearningCertificationPathsRecipeStep>();
        services.AddRecipeExecutionStep<LearningCreditTypesRecipeStep>();
    }
}

/// <summary>
/// Exports certification paths with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LearningCertificationPathsDeploymentSource, LearningCertificationPathsDeploymentStep, LearningCertificationPathsDeploymentStepDisplayDriver>();
        services.AddDeployment<LearningCreditTypesDeploymentSource, LearningCreditTypesDeploymentStep, LearningCreditTypesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the certification path recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningCertificationPathsRecipeStepSchema>();
        services.AddScoped<IRecipeStep, LearningCreditTypesRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningCertificationPathsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<LearningCreditTypesDeploymentStepSchema>();
    }
}

/// <summary>
/// Lets a quiz passed count as a certification requirement.
/// </summary>
[RequireFeatures(AssessmentsConstants.Features.Assessments)]
public sealed class AssessmentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCertificationsAssessments();
    }
}

/// <summary>
/// Adds the certification path workflow events and the Start Certification Path task.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCertificationPathObserver<CertificationWorkflowObserver>();
        services.AddActivity<CertificationRenewalOpenedEvent, CertificationPathEventDisplayDriver<CertificationRenewalOpenedEvent>>();
        services.AddActivity<CertificationLapsedEvent, CertificationPathEventDisplayDriver<CertificationLapsedEvent>>();
        services.AddActivity<StartCertificationPathTask, StartCertificationPathTaskDisplayDriver>();
        services.AddEducationCreditObserver<CreditsWorkflowObserver>();
        services.AddActivity<EducationCreditsAwardedEvent, EducationCreditsAwardedEventDisplayDriver>();
        services.AddActivity<ExternalLearningSubmittedEvent, ExternalLearningSubmittedEventDisplayDriver>();
        services.AddActivity<AwardEducationCreditsTask, AwardEducationCreditsTaskDisplayDriver>();
    }
}

/// <summary>
/// Describes the certification path workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CertificationRenewalOpenedEventSchema>()
            .AddWorkflowActivitySchema<CertificationLapsedEventSchema>()
            .AddWorkflowActivitySchema<StartCertificationPathTaskSchema>()
            .AddWorkflowActivitySchema<EducationCreditsAwardedEventSchema>()
            .AddWorkflowActivitySchema<ExternalLearningSubmittedEventSchema>()
            .AddWorkflowActivitySchema<AwardEducationCreditsTaskSchema>();
    }
}
