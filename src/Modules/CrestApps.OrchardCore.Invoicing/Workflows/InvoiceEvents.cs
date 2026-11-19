using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Invoicing.Workflows;

/// <summary>
/// A workflow event about an invoice. The workflow starts with the invoice's values as input
/// (<c>Workflow.Input.InvoiceId</c>, <c>Workflow.Input.AmountDue</c> and so on), correlated by the invoice's id.
/// </summary>
public abstract class InvoiceEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected InvoiceEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Invoicing"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"])];
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}

/// <summary>
/// The invoice events have no settings, so they only render their thumbnail and design shapes.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class InvoiceEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : InvoiceEventActivity
{
}

/// <summary>
/// Starts when a draft invoice is created.
/// </summary>
public sealed class InvoiceCreatedEvent : InvoiceEventActivity
{
    public InvoiceCreatedEvent(IStringLocalizer<InvoiceCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Created"];
}

/// <summary>
/// Starts when an invoice is issued with its number.
/// </summary>
public sealed class InvoiceIssuedEvent : InvoiceEventActivity
{
    public InvoiceIssuedEvent(IStringLocalizer<InvoiceIssuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceIssuedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Issued"];
}

/// <summary>
/// Starts when an invoice is sent to its customer.
/// </summary>
public sealed class InvoiceSentEvent : InvoiceEventActivity
{
    public InvoiceSentEvent(IStringLocalizer<InvoiceSentEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceSentEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Sent"];
}

/// <summary>
/// Starts the first time the customer opens an invoice's page.
/// </summary>
public sealed class InvoiceViewedEvent : InvoiceEventActivity
{
    public InvoiceViewedEvent(IStringLocalizer<InvoiceViewedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceViewedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Viewed"];
}

/// <summary>
/// Starts when a payment toward an invoice reaches it; the input's <c>Amount</c> is what the payment added.
/// </summary>
public sealed class InvoicePaymentReceivedEvent : InvoiceEventActivity
{
    public InvoicePaymentReceivedEvent(IStringLocalizer<InvoicePaymentReceivedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoicePaymentReceivedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Payment Received"];
}

/// <summary>
/// Starts when an invoice is paid in full.
/// </summary>
public sealed class InvoicePaidEvent : InvoiceEventActivity
{
    public InvoicePaidEvent(IStringLocalizer<InvoicePaidEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoicePaidEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Paid"];
}

/// <summary>
/// Starts the first time an open invoice is found unpaid after its due date.
/// </summary>
public sealed class InvoiceOverdueEvent : InvoiceEventActivity
{
    public InvoiceOverdueEvent(IStringLocalizer<InvoiceOverdueEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceOverdueEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Overdue"];
}

/// <summary>
/// Starts when an invoice is voided; the input's <c>Reason</c> says why.
/// </summary>
public sealed class InvoiceVoidedEvent : InvoiceEventActivity
{
    public InvoiceVoidedEvent(IStringLocalizer<InvoiceVoidedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(InvoiceVoidedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Invoice Voided"];
}

/// <summary>
/// Starts when a credit note for an invoice is issued; the input holds the invoice and the credit note's
/// <c>CreditNoteId</c>, <c>CreditNoteNumber</c>, <c>CreditNoteTotal</c> and <c>Disposition</c>.
/// </summary>
public sealed class CreditNoteIssuedEvent : InvoiceEventActivity
{
    public CreditNoteIssuedEvent(IStringLocalizer<CreditNoteIssuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CreditNoteIssuedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Credit Note Issued"];
}
