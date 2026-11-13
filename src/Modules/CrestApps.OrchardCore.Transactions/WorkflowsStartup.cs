using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Registers the transaction workflow events and tasks when Workflows is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TransactionWorkflowEventHandler>();
        services.AddScoped<ITransactionEventHandler>(sp => sp.GetRequiredService<TransactionWorkflowEventHandler>());
        services.AddScoped<ITransactionPaymentHandler>(sp => sp.GetRequiredService<TransactionWorkflowEventHandler>());

        services.AddActivity<TransactionCreatedEvent, TransactionEventActivityDisplayDriver<TransactionCreatedEvent>>();
        services.AddActivity<TransactionPaymentRecordedEvent, TransactionEventActivityDisplayDriver<TransactionPaymentRecordedEvent>>();
        services.AddActivity<TransactionSettledEvent, TransactionEventActivityDisplayDriver<TransactionSettledEvent>>();
        services.AddActivity<TransactionOverdueEvent, TransactionEventActivityDisplayDriver<TransactionOverdueEvent>>();
        services.AddActivity<TransactionCanceledEvent, TransactionEventActivityDisplayDriver<TransactionCanceledEvent>>();
        services.AddActivity<TransactionRefundedEvent, TransactionEventActivityDisplayDriver<TransactionRefundedEvent>>();

        services.AddActivity<RecordTransactionPaymentTask, TransactionTaskDisplayDriver<RecordTransactionPaymentTask>>();
        services.AddActivity<CancelTransactionTask, TransactionTaskDisplayDriver<CancelTransactionTask>>();
        services.AddActivity<AdjustTransactionTask, TransactionTaskDisplayDriver<AdjustTransactionTask>>();
    }
}
