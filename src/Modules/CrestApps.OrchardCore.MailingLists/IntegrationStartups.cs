using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.MailingLists.ContentTransfer;
using CrestApps.OrchardCore.MailingLists.Deployments;
using CrestApps.OrchardCore.MailingLists.Recipes;
using CrestApps.OrchardCore.MailingLists.Reports;
using CrestApps.OrchardCore.MailingLists.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.MailingLists;

/// <summary>
/// Registers the <c>MailingLists</c> and <c>MailingListMembers</c> recipe steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<MailingListsRecipeStep>();
        services.AddRecipeExecutionStep<MailingListMembersRecipeStep>();
    }
}

/// <summary>
/// Registers the mailing lists and members deployment steps. The members step is personal data and is never part of a
/// plan unless someone adds it.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<MailingListsDeploymentSource, MailingListsDeploymentStep, MailingListsDeploymentStepDisplayDriver>();
        services.AddDeployment<MailingListMembersDeploymentSource, MailingListMembersDeploymentStep, MailingListMembersDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the mailing lists recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, MailingListsRecipeStepSchema>();
        services.AddScoped<IRecipeStep, MailingListMembersRecipeStepSchema>();
        services.AddDeploymentStepSchema<MailingListsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<MailingListMembersDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the mailing list member events and the Subscribe and Unsubscribe tasks, so any form can sign people up.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMailingListMemberObserver<MailingListWorkflowObserver>();
        services.AddActivity<MailingListMemberSubscribedEvent, MailingListMemberSubscribedEventDisplayDriver>();
        services.AddActivity<MailingListMemberVerifiedEvent, MailingListMemberVerifiedEventDisplayDriver>();
        services.AddActivity<MailingListMemberUnsubscribedEvent, MailingListMemberUnsubscribedEventDisplayDriver>();
        services.AddActivity<SubscribeToMailingListTask, SubscribeToMailingListTaskDisplayDriver>();
        services.AddActivity<UnsubscribeFromMailingListTask, UnsubscribeFromMailingListTaskDisplayDriver>();
    }
}

/// <summary>
/// Exposes the members of the mailing lists as a data source of Orchard Core, for reports, their CSV and Excel exports,
/// and data pipelines.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<MailingListsReportDataSource>()
            .AddScoped<MailingListMembersReportDataSet>();
    }
}

/// <summary>
/// Lets the members of mailing lists be imported from a file through ContentTransfer, without sending any e-mail.
/// </summary>
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class ContentTransferStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentTransferImportTarget<MailingListMembersImportTarget>();
    }
}

/// <summary>
/// Describes the mailing list workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<MailingListMemberSubscribedEventSchema>()
            .AddWorkflowActivitySchema<MailingListMemberVerifiedEventSchema>()
            .AddWorkflowActivitySchema<MailingListMemberUnsubscribedEventSchema>()
            .AddWorkflowActivitySchema<SubscribeToMailingListTaskSchema>()
            .AddWorkflowActivitySchema<UnsubscribeFromMailingListTaskSchema>();
    }
}
