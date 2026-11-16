using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
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
        services.AddActivity<PayRunCanceledEvent, PayrollEventActivityDisplayDriver<PayRunCanceledEvent>>();
        services.AddActivity<PayrollFilingPreparedEvent, PayrollEventActivityDisplayDriver<PayrollFilingPreparedEvent>>();
        services.AddActivity<PayrollFilingFiledEvent, PayrollEventActivityDisplayDriver<PayrollFilingFiledEvent>>();
        services.AddActivity<PayrollFilingAmendedEvent, PayrollEventActivityDisplayDriver<PayrollFilingAmendedEvent>>();
        services.AddActivity<PayrollYearClosedEvent, PayrollEventActivityDisplayDriver<PayrollYearClosedEvent>>();
        services.AddActivity<PayrollYearReopenedEvent, PayrollEventActivityDisplayDriver<PayrollYearReopenedEvent>>();
        services.AddActivity<TaxDepositRecordedEvent, PayrollEventActivityDisplayDriver<TaxDepositRecordedEvent>>();

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
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayrollFilingObserver, PayrollWorkflowObserver>(provider => provider.GetRequiredService<PayrollWorkflowObserver>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxDeposit>, TaxDepositWorkflowHandler>());
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
            .AddWorkflowActivitySchema<PayRunCanceledEventSchema>()
            .AddWorkflowActivitySchema<PayrollFilingPreparedEventSchema>()
            .AddWorkflowActivitySchema<PayrollFilingFiledEventSchema>()
            .AddWorkflowActivitySchema<PayrollFilingAmendedEventSchema>()
            .AddWorkflowActivitySchema<PayrollYearClosedEventSchema>()
            .AddWorkflowActivitySchema<PayrollYearReopenedEventSchema>()
            .AddWorkflowActivitySchema<TaxDepositRecordedEventSchema>()
            .AddWorkflowActivitySchema<OpenPayRunTaskSchema>()
            .AddWorkflowActivitySchema<AddPayInputTaskSchema>()
            .AddWorkflowActivitySchema<CalculatePayRunTaskSchema>()
            .AddWorkflowActivitySchema<SubmitPayRunTaskSchema>()
            .AddWorkflowActivitySchema<ApprovePayRunTaskSchema>()
            .AddWorkflowActivitySchema<RejectPayRunTaskSchema>()
            .AddWorkflowActivitySchema<ProcessPayRunTaskSchema>();
    }
}

/// <summary>
/// The garnishment workflow events: an order added, what it withheld and sent, a remittance voided and an order completed.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class GarnishmentsWorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<GarnishmentOrderCreatedEvent, PayrollEventActivityDisplayDriver<GarnishmentOrderCreatedEvent>>();
        services.AddActivity<GarnishmentWithheldEvent, PayrollEventActivityDisplayDriver<GarnishmentWithheldEvent>>();
        services.AddActivity<GarnishmentRemittanceVoidedEvent, PayrollEventActivityDisplayDriver<GarnishmentRemittanceVoidedEvent>>();
        services.AddActivity<GarnishmentOrderCompletedEvent, PayrollEventActivityDisplayDriver<GarnishmentOrderCompletedEvent>>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGarnishmentObserver, GarnishmentsWorkflowObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<GarnishmentOrder>, GarnishmentOrderWorkflowHandler>());
    }
}

/// <summary>
/// The recipe schemas of the garnishment workflow events.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class GarnishmentsWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<GarnishmentOrderCreatedEventSchema>()
            .AddWorkflowActivitySchema<GarnishmentWithheldEventSchema>()
            .AddWorkflowActivitySchema<GarnishmentRemittanceVoidedEventSchema>()
            .AddWorkflowActivitySchema<GarnishmentOrderCompletedEventSchema>();
    }
}
