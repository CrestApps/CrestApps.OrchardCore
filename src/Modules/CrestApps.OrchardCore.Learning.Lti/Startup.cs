using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Deployments;
using CrestApps.OrchardCore.Learning.Lti.Drivers;
using CrestApps.OrchardCore.Learning.Lti.Handlers;
using CrestApps.OrchardCore.Learning.Lti.Migrations;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Navigation;
using CrestApps.OrchardCore.Learning.Lti.Recipes;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.Workflows;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Lti;

/// <summary>
/// Registers External Tools: the tool records and their admin pages, the External tool course item with its editor and
/// learner panel, the launch, the platform key on the shared signing keys, and the permission.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInvariantNumberInputs();
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(LtiConstants.CollectionName));
        services.AddDataMigration<LtiMigrations>()
            .AddIndexProvider<ExternalToolIndexProvider>()
            .AddIndexProvider<LtiLineItemIndexProvider>()
            .AddIndexProvider<LtiScoreIndexProvider>();

        services.AddSigningKeys();
        services.AddLearningLtiCore();
        services.AddScoped<ICatalogEntryHandler<ExternalTool>, ExternalToolHandler>();
        services.AddScoped<ExternalToolClients>();
        services.AddScoped<LtiDeepLinkItemWriter>();
        services.AddDisplayDriver<ExternalTool, ExternalToolDisplayDriver>();

        services.AddContentPart<ExternalToolItemPart>()
            .UseDisplayDriver<ExternalToolItemPartDisplayDriver>()
            .UseDisplayDriver<ExternalToolItemPartLearnerDisplayDriver>();

        services.AddContentPart<CoursePart>()
            .UseDisplayDriver<CourseExternalToolsDisplayDriver>();

        services.AddPermissionProvider<LtiPermissionProvider>();
        services.AddPersonalDataContributor<LtiPersonalDataContributor>();
        services.AddNavigationProvider<LtiAdminMenu>();
    }
}

/// <summary>
/// Imports external tool settings with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<LearningExternalToolsRecipeStep>();
}

/// <summary>
/// Exports external tool settings with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<LearningExternalToolsDeploymentSource, LearningExternalToolsDeploymentStep, LearningExternalToolsDeploymentStepDisplayDriver>();
}

/// <summary>
/// Describes the external tools recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningExternalToolsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningExternalToolsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the External Score Received workflow event.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ExternalScoreReceivedEvent, ExternalScoreReceivedEventDisplayDriver>();
        services.AddLtiScoreObserver<LtiScoreWorkflowObserver>();
    }
}

/// <summary>
/// Describes the External Score Received event to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddWorkflowActivitySchema<ExternalScoreReceivedEventSchema>();
}
