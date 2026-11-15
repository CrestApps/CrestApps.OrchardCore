using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Payroll's workflow events and tasks. Payroll adds no activity for what Orchard has: a schedule is Timer Event, a notice
/// NotifyUserTask, an export Run Data Pipeline Task. A returned deposit starts Disbursements' Disbursement Returned event
/// (source kind PayStatement) and a changed bank account Banking's Bank Account Changed event.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class PayrollWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<PayRunOpenedEvent, PayrollEventActivityDisplayDriver<PayRunOpenedEvent>>();
        services.AddActivity<PayRunCalculatedEvent, PayrollEventActivityDisplayDriver<PayRunCalculatedEvent>>();
        services.AddActivity<PayRunSubmittedEvent, PayrollEventActivityDisplayDriver<PayRunSubmittedEvent>>();
        services.AddActivity<PayRunApprovedEvent, PayrollEventActivityDisplayDriver<PayRunApprovedEvent>>();
        services.AddActivity<PayRunRejectedEvent, PayrollEventActivityDisplayDriver<PayRunRejectedEvent>>();
        services.AddActivity<PayRunProcessedEvent, PayrollEventActivityDisplayDriver<PayRunProcessedEvent>>();
        services.AddActivity<PayRunPaidEvent, PayrollEventActivityDisplayDriver<PayRunPaidEvent>>();
        services.AddActivity<PayRunFailedEvent, PayrollEventActivityDisplayDriver<PayRunFailedEvent>>();
        services.AddActivity<PayStatementIssuedEvent, PayrollEventActivityDisplayDriver<PayStatementIssuedEvent>>();
        services.AddActivity<PayStatementVoidedEvent, PayrollEventActivityDisplayDriver<PayStatementVoidedEvent>>();
        services.AddActivity<PayProfileChangedEvent, PayrollEventActivityDisplayDriver<PayProfileChangedEvent>>();

        services.AddActivity<OpenPayRunTask, PayrollTaskDisplayDriver<OpenPayRunTask>>();
        services.AddActivity<AddPayInputTask, PayrollTaskDisplayDriver<AddPayInputTask>>();
        services.AddActivity<CalculatePayRunTask, PayrollTaskDisplayDriver<CalculatePayRunTask>>();
        services.AddActivity<SubmitPayRunTask, PayrollTaskDisplayDriver<SubmitPayRunTask>>();
        services.AddActivity<ApprovePayRunTask, PayrollTaskDisplayDriver<ApprovePayRunTask>>();
        services.AddActivity<RejectPayRunTask, PayrollTaskDisplayDriver<RejectPayRunTask>>();
        services.AddActivity<ProcessPayRunTask, PayrollTaskDisplayDriver<ProcessPayRunTask>>();

        services.TryAddScoped<PayrollWorkflowObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayRunObserver, PayrollWorkflowObserver>(provider => provider.GetRequiredService<PayrollWorkflowObserver>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementObserver, PayrollWorkflowObserver>(provider => provider.GetRequiredService<PayrollWorkflowObserver>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayProfileObserver, PayrollWorkflowObserver>(provider => provider.GetRequiredService<PayrollWorkflowObserver>()));
    }
}

/// <summary>
/// The recipe schemas of Payroll's workflow activities.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class PayrollWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<PayRunOpenedEventSchema>()
            .AddWorkflowActivitySchema<PayRunCalculatedEventSchema>()
            .AddWorkflowActivitySchema<PayRunSubmittedEventSchema>()
            .AddWorkflowActivitySchema<PayRunApprovedEventSchema>()
            .AddWorkflowActivitySchema<PayRunRejectedEventSchema>()
            .AddWorkflowActivitySchema<PayRunProcessedEventSchema>()
            .AddWorkflowActivitySchema<PayRunPaidEventSchema>()
            .AddWorkflowActivitySchema<PayRunFailedEventSchema>()
            .AddWorkflowActivitySchema<PayStatementIssuedEventSchema>()
            .AddWorkflowActivitySchema<PayStatementVoidedEventSchema>()
            .AddWorkflowActivitySchema<PayProfileChangedEventSchema>()
            .AddWorkflowActivitySchema<OpenPayRunTaskSchema>()
            .AddWorkflowActivitySchema<AddPayInputTaskSchema>()
            .AddWorkflowActivitySchema<CalculatePayRunTaskSchema>()
            .AddWorkflowActivitySchema<SubmitPayRunTaskSchema>()
            .AddWorkflowActivitySchema<ApprovePayRunTaskSchema>()
            .AddWorkflowActivitySchema<RejectPayRunTaskSchema>()
            .AddWorkflowActivitySchema<ProcessPayRunTaskSchema>();
    }
}
