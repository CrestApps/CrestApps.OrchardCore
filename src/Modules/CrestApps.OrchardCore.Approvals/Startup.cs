using CrestApps.OrchardCore.Approvals.Core.Handlers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Core;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Deployments;
using CrestApps.OrchardCore.Approvals.Drivers;
using CrestApps.OrchardCore.Approvals.Indexes;
using CrestApps.OrchardCore.Approvals.Migrations;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Navigation;
using CrestApps.OrchardCore.Approvals.Recipes;
using CrestApps.OrchardCore.Approvals.Reports;
using CrestApps.OrchardCore.Approvals.ViewModels;
using CrestApps.OrchardCore.Approvals.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Approvals;

/// <summary>
/// Registers approval requests, policies and their admin pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ApprovalsConstants.CollectionName));
        services.AddDataMigration<ApprovalsMigrations>()
            .AddIndexProvider<ApprovalRequestIndexProvider>();

        services.AddApprovalsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ApprovalPolicy>, ApprovalPolicyHandler>());
        services.AddHttpContextAccessor();
        services.AddDisplayDriver<ApprovalPolicy, ApprovalPolicyDisplayDriver>();
        services.AddDisplayDriver<ApprovalPolicy, ApprovalPolicyConditionsDisplayDriver>();
        services.AddDisplayDriver<ApprovalRequest, ApprovalRequestDisplayDriver>();
        services.AddDisplayDriver<ApprovalDecideViewModel, ApprovalDecideDisplayDriver>();
        services.AddPermissionProvider<ApprovalsPermissionProvider>();
        services.AddNavigationProvider<ApprovalsAdminMenu>();
    }
}

/// <summary>
/// The approver rules of Staff: the subject's manager and named staff members.
/// </summary>
[RequireFeatures(CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApproverRuleProvider<Services.ManagerApproverRuleProvider>();
        services.AddApproverRuleProvider<Services.StaffApproverRuleProvider>();
    }
}

/// <summary>
/// Makes Orchard's notification center the approver's inbox: two notification types each person chooses delivery
/// for, and the observer that sends and removes the notices.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            ApprovalsConstants.ApprovalNotificationType,
            LocalizationSource.Create<NotificationsStartup>("Approvals to decide"),
            type => type.Description = LocalizationSource.Create<NotificationsStartup>("When someone asks for your approval."));
        services.AddNotificationType(
            ApprovalsConstants.DecisionNotificationType,
            LocalizationSource.Create<NotificationsStartup>("Approval decisions"),
            type => type.Description = LocalizationSource.Create<NotificationsStartup>("When a request you made is approved or rejected."));
        services.AddApprovalObserver<ApprovalNotificationObserver>();
    }
}

/// <summary>
/// Starts workflows when approvals are asked for and decided, and asks, approves and rejects from tasks.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ApprovalRequestedEvent, ApprovalEventActivityDisplayDriver<ApprovalRequestedEvent>>();
        services.AddActivity<ApprovalApprovedEvent, ApprovalEventActivityDisplayDriver<ApprovalApprovedEvent>>();
        services.AddActivity<ApprovalRejectedEvent, ApprovalEventActivityDisplayDriver<ApprovalRejectedEvent>>();
        services.AddActivity<ApprovalPartlyApprovedEvent, ApprovalEventActivityDisplayDriver<ApprovalPartlyApprovedEvent>>();
        services.AddActivity<ApprovalStageApprovedEvent, ApprovalEventActivityDisplayDriver<ApprovalStageApprovedEvent>>();
        services.AddActivity<ApprovalCanceledEvent, ApprovalEventActivityDisplayDriver<ApprovalCanceledEvent>>();
        services.AddActivity<RequestApprovalTask, ApprovalTaskDisplayDriver<RequestApprovalTask>>();
        services.AddActivity<ApproveRequestTask, ApprovalTaskDisplayDriver<ApproveRequestTask>>();
        services.AddActivity<RejectRequestTask, ApprovalTaskDisplayDriver<RejectRequestTask>>();
        services.AddActivity<GetApprovalRequestTask, ApprovalTaskDisplayDriver<GetApprovalRequestTask>>();
        services.AddApprovalObserver<ApprovalsWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Approvals workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<ApprovalRequestedEventSchema>()
            .AddWorkflowActivitySchema<ApprovalApprovedEventSchema>()
            .AddWorkflowActivitySchema<ApprovalRejectedEventSchema>()
            .AddWorkflowActivitySchema<ApprovalPartlyApprovedEventSchema>()
            .AddWorkflowActivitySchema<ApprovalStageApprovedEventSchema>()
            .AddWorkflowActivitySchema<ApprovalCanceledEventSchema>()
            .AddWorkflowActivitySchema<RequestApprovalTaskSchema>()
            .AddWorkflowActivitySchema<ApproveRequestTaskSchema>()
            .AddWorkflowActivitySchema<RejectRequestTaskSchema>()
            .AddWorkflowActivitySchema<GetApprovalRequestTaskSchema>();
    }
}

/// <summary>
/// Imports approval policies and approval history with recipe steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ApprovalPoliciesRecipeStep>();
        services.AddRecipeExecutionStep<ApprovalRequestsRecipeStep>();
    }
}

/// <summary>
/// Exports approval policies and (opt-in) approval requests with deployment steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ApprovalPoliciesDeploymentSource, ApprovalPoliciesDeploymentStep>();
        services.AddDeployment<ApprovalRequestsDeploymentSource, ApprovalRequestsDeploymentStep, ApprovalRequestsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Approvals recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ApprovalPoliciesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, ApprovalRequestsRecipeStepSchema>();
        services.AddDeploymentStepSchema<ApprovalPoliciesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<ApprovalRequestsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Approvals data source.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<ApprovalsReportDataSource>();
        services.AddScoped<IApprovalsReportDataSet, ApprovalRequestsReportDataSet>();
    }
}

/// <summary>
/// Writes approval policy changes to the Audit Trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<Microsoft.Extensions.Options.IConfigureOptions<global::OrchardCore.AuditTrail.Services.Models.AuditTrailOptions>, AuditTrail.ApprovalsAuditTrailEvents>();
        services.AddScoped<CrestApps.Core.Services.ICatalogEntryHandler<ApprovalPolicy>, AuditTrail.ApprovalPolicyAuditTrailHandler>();
    }
}
