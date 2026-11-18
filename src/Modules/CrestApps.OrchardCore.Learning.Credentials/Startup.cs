using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Drivers;
using CrestApps.OrchardCore.Learning.Credentials.Migrations;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Navigation;
using CrestApps.OrchardCore.Learning.Credentials.Recipes;
using CrestApps.OrchardCore.Learning.Credentials.Reports;
using CrestApps.OrchardCore.Learning.Credentials.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using CrestApps.OrchardCore.Learning.Credentials.Workflows;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Credentials;

/// <summary>
/// Registers Digital Credentials.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CredentialsConstants.CollectionName));
        services.AddDataMigration<CredentialsMigrations>()
            .AddIndexProvider<CredentialAwardIndexProvider>();

        services.AddSigningKeys();
        services.AddLearningCredentialsCore();
        services.AddScoped<ICredentialDefinitionCatalog, ContentCredentialDefinitionCatalog>();
        services.AddContentPart<CredentialDefinitionPart>()
            .UseDisplayDriver<CredentialDefinitionPartDisplayDriver>();
        services.AddCredentialRuleKind<CourseCompletedRuleKind>();
        services.AddScoped<IAuthorizationHandler, CredentialDefinitionAuthorizationHandler>();
        services.AddPermissionProvider<CredentialsPermissionProvider>();
        services.AddNavigationProvider<CredentialsAdminMenu>();
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDisplayDriver<CredentialAwardRow, CredentialAwardRowDisplayDriver>();
        services.AddPersonalDataContributor<CredentialsPersonalDataContributor>();
        services.AddScoped<IEnrollmentReviewActionProvider, CredentialReviewActionProvider>();
    }
}

/// <summary>
/// My credentials in the storefront account menu, or in the user menu when the storefront account is off.
/// </summary>
[RequireFeatures(LearningConstants.Features.LearnerPages)]
public sealed class LearnerPagesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CredentialsAccountMenu>();
        services.AddDisplayDriver<UserMenu, CredentialsUserMenuDisplayDriver>();
    }
}

/// <summary>
/// The quiz passed rule, when Assessments is on.
/// </summary>
[RequireFeatures(AssessmentsConstants.Features.Assessments)]
public sealed class AssessmentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsAssessments();
        services.AddCredentialRuleKind<QuizPassedRuleKind>();
    }
}

/// <summary>
/// The learning path completed rule, when Learning Paths is on.
/// </summary>
[RequireFeatures(ProgramsConstants.Features.Paths)]
public sealed class ProgramsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsPrograms();
        services.AddCredentialRuleKind<LearningPathCompletedRuleKind>();
    }
}

/// <summary>
/// The certification issued rule, when Learning Certifications is on.
/// </summary>
[RequireFeatures(CertificationsConstants.Features.Certifications)]
public sealed class CertificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsCertifications();
        services.AddCredentialRuleKind<CertificationIssuedRuleKind>();
    }
}

/// <summary>
/// The Credential awarded notification, when Notifications is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(CredentialsConstants.NotificationType, LocalizationSource.Create<NotificationsStartup>("Credential awarded"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When you earn a digital credential."));
        services.AddCredentialAwardObserver<CredentialNotificationObserver>();
    }
}

/// <summary>
/// The Credential awards data set, when Data Pipelines is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CredentialAwardsReportDataSet>();
        services.AddDataSource<LearningCredentialsDataSource>();
    }
}

/// <summary>
/// The LearningCredentialAwards recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<LearningCredentialAwardsRecipeStep>();
}

/// <summary>
/// The LearningCredentialAwards deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<LearningCredentialAwardsDeploymentSource, LearningCredentialAwardsDeploymentStep, LearningCredentialAwardsDeploymentStepDisplayDriver>();
}

/// <summary>
/// The CrestApps Recipes schemas of the recipe and deployment steps.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningCredentialAwardsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningCredentialAwardsDeploymentStepSchema>();
    }
}

/// <summary>
/// The Credential Awarded event and the Award Credential task, when Workflows is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CredentialAwardedEvent, CredentialAwardedEventDisplayDriver>();
        services.AddActivity<AwardCredentialTask, AwardCredentialTaskDisplayDriver>();
        services.AddCredentialAwardObserver<CredentialWorkflowObserver>();
    }
}

/// <summary>
/// The CrestApps Recipes schemas of the workflow activities.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services
            .AddWorkflowActivitySchema<CredentialAwardedEventSchema>()
            .AddWorkflowActivitySchema<AwardCredentialTaskSchema>();
}
