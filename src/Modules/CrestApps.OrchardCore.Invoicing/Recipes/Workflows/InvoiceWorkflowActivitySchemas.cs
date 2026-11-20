using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Invoicing.Recipes.Workflows;

/// <summary>
/// Describes the invoice workflow events, which have no properties.
/// </summary>
internal abstract class InvoiceEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Invoicing";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the properties every invoice workflow task shares.
/// </summary>
internal abstract class InvoiceTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string TextHint = " A plain string that supports Liquid.";

    protected override string Category => "Invoicing";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["InvoiceId"];

    protected virtual bool HasInvoiceId => true;

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        if (HasInvoiceId)
        {
            yield return ("InvoiceId", Text("The id of the invoice."));
        }

        foreach (var property in GetTaskProperties())
        {
            yield return property;
        }

        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The name of the workflow output that receives the invoice and the command's errors. Defaults to 'Invoice'."));
    }

    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties();

    protected static JsonSchemaBuilder Text(string description)
        => WorkflowActivitySchemaBuilders.String(description + TextHint);
}

internal sealed class InvoiceCreatedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceCreatedEvent";

    protected override string DisplayText => "Invoice Created";

    protected override string Description => "Starts when a draft invoice is created";
}

internal sealed class InvoiceIssuedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceIssuedEvent";

    protected override string DisplayText => "Invoice Issued";

    protected override string Description => "Starts when an invoice is issued with its number";
}

internal sealed class InvoiceSentEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceSentEvent";

    protected override string DisplayText => "Invoice Sent";

    protected override string Description => "Starts when an invoice is sent to its customer";
}

internal sealed class InvoiceViewedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceViewedEvent";

    protected override string DisplayText => "Invoice Viewed";

    protected override string Description => "Starts the first time the customer opens an invoice";
}

internal sealed class InvoicePaymentReceivedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoicePaymentReceivedEvent";

    protected override string DisplayText => "Invoice Payment Received";

    protected override string Description => "Starts when a payment toward an invoice reaches it";
}

internal sealed class InvoicePaidEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoicePaidEvent";

    protected override string DisplayText => "Invoice Paid";

    protected override string Description => "Starts when an invoice is paid in full";
}

internal sealed class InvoiceOverdueEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceOverdueEvent";

    protected override string DisplayText => "Invoice Overdue";

    protected override string Description => "Starts the first time an open invoice is found unpaid after its due date";
}

internal sealed class InvoiceVoidedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "InvoiceVoidedEvent";

    protected override string DisplayText => "Invoice Voided";

    protected override string Description => "Starts when an invoice is voided";
}

internal sealed class CreditNoteIssuedEventSchema : InvoiceEventSchemaBase
{
    public override string Name => "CreditNoteIssuedEvent";

    protected override string DisplayText => "Credit Note Issued";

    protected override string Description => "Starts when a credit note for an invoice is issued";
}

internal sealed class CreateInvoiceTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "CreateInvoiceTask";

    protected override string DisplayText => "Create Invoice";

    protected override string Description => "Creates a draft invoice with one line for a bill-to party";

    protected override bool HasInvoiceId => false;

    protected override IEnumerable<string> RequiredProperties => ["BillTo", "Description", "Amount"];

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("BillTo", Text("The party billed, written Kind:Id (such as Account:123 or User:456)."));
        yield return ("Currency", Text("The currency code. Optional; the bill-to's or the site's default when empty."));
        yield return ("Description", Text("The name of the invoice's line."));
        yield return ("Amount", Text("The line's amount, with a dot as the decimal separator."));
        yield return ("Reference", Text("A reference kept on the invoice, such as an order number. Optional."));
    }
}

internal sealed class IssueInvoiceTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "IssueInvoiceTask";

    protected override string DisplayText => "Issue Invoice";

    protected override string Description => "Issues a draft invoice: numbers it and posts it to the ledger";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
        => [];
}

internal sealed class SendInvoiceTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "SendInvoiceTask";

    protected override string DisplayText => "Send Invoice";

    protected override string Description => "Sends an invoice to its customer by e-mail with its PDF";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("To", Text("The address it is sent to. Optional; the bill-to's e-mail when empty."));
    }
}

internal sealed class RecordInvoicePaymentTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "RecordInvoicePaymentTask";

    protected override string DisplayText => "Record Invoice Payment";

    protected override string Description => "Records a payment received outside the site toward an open invoice";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Amount", Text("The amount received, with a dot as the decimal separator. Optional; what is still due when empty."));
        yield return ("PaymentMethod", Text("How the payment was made, such as Bank transfer. Optional."));
        yield return ("Reference", Text("The payment's reference, such as a check number. Optional."));
        yield return ("Note", Text("A note kept on the transaction's timeline. Optional."));
    }
}

internal sealed class VoidInvoiceTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "VoidInvoiceTask";

    protected override string DisplayText => "Void Invoice";

    protected override string Description => "Voids an open invoice nothing was paid toward";

    protected override IEnumerable<string> RequiredProperties => ["InvoiceId", "Reason"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Reason", Text("Why the invoice is voided."));
    }
}

internal sealed class CreateCreditNoteTaskSchema : InvoiceTaskSchemaBase
{
    public override string Name => "CreateCreditNoteTask";

    protected override string DisplayText => "Create Credit Note";

    protected override string Description => "Credits everything still creditable on an invoice with a credit note, and issues it";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("Reason", Text("Why the invoice is credited. Optional."));
        yield return ("Disposition", Text("What happens to the credited amount: ReduceBalance, Refund or CustomerCredit. Optional; the default for the invoice when empty."));
    }
}
