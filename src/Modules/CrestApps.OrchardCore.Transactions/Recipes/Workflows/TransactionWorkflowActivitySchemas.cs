using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Transactions.Recipes.Workflows;

/// <summary>
/// Describes the filters every transaction workflow event shares.
/// </summary>
internal abstract class TransactionEventActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Transactions";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Sources", WorkflowActivitySchemaBuilders.String("A comma-separated list of the transaction sources (such as a payment provider key) that start the workflow, matched without regard to case. Empty for every source."));
        yield return ("ReferenceTypes", WorkflowActivitySchemaBuilders.String("A comma-separated list of the reference types (such as Invoice or Order) whose transactions start the workflow, matched without regard to case. Empty for every type."));
    }
}

/// <summary>
/// Describes the properties every transaction workflow task shares.
/// </summary>
internal abstract class TransactionTaskActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string TextHint = " A plain string that supports Liquid.";

    protected override string Category => "Transactions";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["TransactionId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("TransactionId", Text("The id of the transaction."));

        foreach (var property in GetTaskProperties())
        {
            yield return property;
        }

        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The name of the workflow output that receives the transaction and the command's errors. Defaults to 'Transaction'."));
    }

    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties();

    protected static JsonSchemaBuilder Text(string description)
        => WorkflowActivitySchemaBuilders.String(description + TextHint);
}

internal sealed class TransactionCreatedEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionCreatedEvent";

    protected override string DisplayText => "Transaction Created";

    protected override string Description => "Starts when a transaction is recorded";
}

internal sealed class TransactionPaymentRecordedEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionPaymentRecordedEvent";

    protected override string DisplayText => "Transaction Payment Recorded";

    protected override string Description => "Starts when a payment is applied to a transaction, online or recorded by staff";
}

internal sealed class TransactionSettledEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionSettledEvent";

    protected override string DisplayText => "Transaction Settled";

    protected override string Description => "Starts when nothing more is owed on a transaction";
}

internal sealed class TransactionOverdueEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionOverdueEvent";

    protected override string DisplayText => "Transaction Overdue";

    protected override string Description => "Starts the first time a transaction is found unpaid after its due date";
}

internal sealed class TransactionCanceledEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionCanceledEvent";

    protected override string DisplayText => "Transaction Canceled";

    protected override string Description => "Starts when a transaction is canceled";
}

internal sealed class TransactionRefundedEventSchema : TransactionEventActivitySchemaBase
{
    public override string Name => "TransactionRefundedEvent";

    protected override string DisplayText => "Transaction Refunded";

    protected override string Description => "Starts when money is given back on a transaction";
}

internal sealed class RecordTransactionPaymentTaskSchema : TransactionTaskActivitySchemaBase
{
    public override string Name => "RecordTransactionPaymentTask";

    protected override string DisplayText => "Record Transaction Payment";

    protected override string Description => "Records a payment received outside the site on a transaction";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Amount", Text("The amount received, with a dot as the decimal separator."));
        yield return ("PaymentMethod", Text("How the payment was made, such as Bank transfer. Optional."));
        yield return ("Reference", Text("The payment's reference, such as a check number. Optional."));
        yield return ("Note", Text("A note kept on the transaction's timeline. Optional."));
    }
}

internal sealed class CancelTransactionTaskSchema : TransactionTaskActivitySchemaBase
{
    public override string Name => "CancelTransactionTask";

    protected override string DisplayText => "Cancel Transaction";

    protected override string Description => "Cancels a transaction that is no longer owed";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Reason", Text("Why the transaction is canceled. Optional."));
    }
}

internal sealed class AdjustTransactionTaskSchema : TransactionTaskActivitySchemaBase
{
    public override string Name => "AdjustTransactionTask";

    protected override string DisplayText => "Adjust Transaction";

    protected override string Description => "Takes an amount off what is owed on a transaction: a discount, waiver, credit or write-off";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Amount", Text("The amount taken off, with a dot as the decimal separator."));
        yield return ("AdjustmentKind", Text("Discount, Waiver, Credit or WriteOff."));
        yield return ("Reason", Text("Why the amount is taken off. Optional."));
    }
}
