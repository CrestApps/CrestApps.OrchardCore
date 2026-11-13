using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Transactions.Recipes.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions;

/// <summary>
/// Describes the transaction workflow events and tasks to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<TransactionCreatedEventSchema>()
            .AddWorkflowActivitySchema<TransactionPaymentRecordedEventSchema>()
            .AddWorkflowActivitySchema<TransactionSettledEventSchema>()
            .AddWorkflowActivitySchema<TransactionOverdueEventSchema>()
            .AddWorkflowActivitySchema<TransactionCanceledEventSchema>()
            .AddWorkflowActivitySchema<TransactionRefundedEventSchema>()
            .AddWorkflowActivitySchema<TransactionEntryReversedEventSchema>()
            .AddWorkflowActivitySchema<RecordTransactionPaymentTaskSchema>()
            .AddWorkflowActivitySchema<CancelTransactionTaskSchema>()
            .AddWorkflowActivitySchema<AdjustTransactionTaskSchema>();
    }
}
