using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Payroll.Workflows;

/// <summary>
/// The base of Payroll's events: started by <see cref="PayrollWorkflowObserver"/> once the change is committed.
/// </summary>
public abstract class PayrollEventActivity : EventActivity
{
    protected PayrollEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Payroll"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// The editor of Payroll's events: they have no settings.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class PayrollEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : PayrollEventActivity
{
}

/// <summary>
/// Starts when a pay run is added.
/// </summary>
public sealed class PayRunOpenedEvent : PayrollEventActivity
{
    public PayRunOpenedEvent(IStringLocalizer<PayRunOpenedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunOpenedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Opened"];
}

/// <summary>
/// Starts when a pay run is calculated.
/// </summary>
public sealed class PayRunCalculatedEvent : PayrollEventActivity
{
    public PayRunCalculatedEvent(IStringLocalizer<PayRunCalculatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunCalculatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Calculated"];
}

/// <summary>
/// Starts when a pay run is submitted for approval.
/// </summary>
public sealed class PayRunSubmittedEvent : PayrollEventActivity
{
    public PayRunSubmittedEvent(IStringLocalizer<PayRunSubmittedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunSubmittedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Submitted for Approval"];
}

/// <summary>
/// Starts when a pay run is approved.
/// </summary>
public sealed class PayRunApprovedEvent : PayrollEventActivity
{
    public PayRunApprovedEvent(IStringLocalizer<PayRunApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunApprovedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Approved"];
}

/// <summary>
/// Starts when a pay run's approval is rejected; Note is the approver's.
/// </summary>
public sealed class PayRunRejectedEvent : PayrollEventActivity
{
    public PayRunRejectedEvent(IStringLocalizer<PayRunRejectedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunRejectedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Rejected"];
}

/// <summary>
/// Starts when a pay run is processed, paid in full or not; Status is Paid, PartiallyPaid or Failed.
/// </summary>
public sealed class PayRunProcessedEvent : PayrollEventActivity
{
    public PayRunProcessedEvent(IStringLocalizer<PayRunProcessedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunProcessedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Processed"];
}

/// <summary>
/// Starts when every statement of a processed pay run is paid.
/// </summary>
public sealed class PayRunPaidEvent : PayrollEventActivity
{
    public PayRunPaidEvent(IStringLocalizer<PayRunPaidEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunPaidEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Paid"];
}

/// <summary>
/// Starts when a processed pay run could pay nothing.
/// </summary>
public sealed class PayRunFailedEvent : PayrollEventActivity
{
    public PayRunFailedEvent(IStringLocalizer<PayRunFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunFailedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Failed"];
}

/// <summary>
/// Starts when a pay statement becomes final and its payments are made.
/// </summary>
public sealed class PayStatementIssuedEvent : PayrollEventActivity
{
    public PayStatementIssuedEvent(IStringLocalizer<PayStatementIssuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayStatementIssuedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Statement Issued"];
}

/// <summary>
/// Starts when a pay statement is voided; ReversalId is the reversing statement.
/// </summary>
public sealed class PayStatementVoidedEvent : PayrollEventActivity
{
    public PayStatementVoidedEvent(IStringLocalizer<PayStatementVoidedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayStatementVoidedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Statement Voided"];
}

/// <summary>
/// Starts when a pay profile changes, such as its tax id, a tax election or where net pay goes; Change says what.
/// </summary>
public sealed class PayProfileChangedEvent : PayrollEventActivity
{
    public PayProfileChangedEvent(IStringLocalizer<PayProfileChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayProfileChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Profile Changed"];
}

/// <summary>
/// Starts when a pay run is cancelled; Note is the reason.
/// </summary>
public sealed class PayRunCanceledEvent : PayrollEventActivity
{
    public PayRunCanceledEvent(IStringLocalizer<PayRunCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayRunCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Pay Run Canceled"];
}

/// <summary>
/// Starts when a tax filing draft is prepared, or prepared again.
/// </summary>
public sealed class PayrollFilingPreparedEvent : PayrollEventActivity
{
    public PayrollFilingPreparedEvent(IStringLocalizer<PayrollFilingPreparedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayrollFilingPreparedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Payroll Filing Prepared"];
}

/// <summary>
/// Starts when a tax filing is marked as filed.
/// </summary>
public sealed class PayrollFilingFiledEvent : PayrollEventActivity
{
    public PayrollFilingFiledEvent(IStringLocalizer<PayrollFilingFiledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayrollFilingFiledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Payroll Filing Filed"];
}

/// <summary>
/// Starts when an amendment of a filed tax filing is started; AmendsId is the filing it corrects.
/// </summary>
public sealed class PayrollFilingAmendedEvent : PayrollEventActivity
{
    public PayrollFilingAmendedEvent(IStringLocalizer<PayrollFilingAmendedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayrollFilingAmendedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Payroll Filing Amended"];
}

/// <summary>
/// Starts when an employer's payroll year is closed.
/// </summary>
public sealed class PayrollYearClosedEvent : PayrollEventActivity
{
    public PayrollYearClosedEvent(IStringLocalizer<PayrollYearClosedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayrollYearClosedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Payroll Year Closed"];
}

/// <summary>
/// Starts when an employer's closed payroll year is reopened.
/// </summary>
public sealed class PayrollYearReopenedEvent : PayrollEventActivity
{
    public PayrollYearReopenedEvent(IStringLocalizer<PayrollYearReopenedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(PayrollYearReopenedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Payroll Year Reopened"];
}

/// <summary>
/// Starts when a tax deposit is recorded.
/// </summary>
public sealed class TaxDepositRecordedEvent : PayrollEventActivity
{
    public TaxDepositRecordedEvent(IStringLocalizer<TaxDepositRecordedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(TaxDepositRecordedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Tax Deposit Recorded"];
}

/// <summary>
/// Starts when a garnishment order is added.
/// </summary>
public sealed class GarnishmentOrderCreatedEvent : PayrollEventActivity
{
    public GarnishmentOrderCreatedEvent(IStringLocalizer<GarnishmentOrderCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(GarnishmentOrderCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Garnishment Order Created"];
}

/// <summary>
/// Starts when a garnishment order withholds from a pay statement and sends it to the payee; Problem says when the payment
/// could not be made.
/// </summary>
public sealed class GarnishmentWithheldEvent : PayrollEventActivity
{
    public GarnishmentWithheldEvent(IStringLocalizer<GarnishmentWithheldEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(GarnishmentWithheldEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Garnishment Withheld"];
}

/// <summary>
/// Starts when a garnishment remittance is voided with its pay statement and the payee's payment is stopped.
/// </summary>
public sealed class GarnishmentRemittanceVoidedEvent : PayrollEventActivity
{
    public GarnishmentRemittanceVoidedEvent(IStringLocalizer<GarnishmentRemittanceVoidedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(GarnishmentRemittanceVoidedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Garnishment Remittance Voided"];
}

/// <summary>
/// Starts when a garnishment order owes nothing more and is completed.
/// </summary>
public sealed class GarnishmentOrderCompletedEvent : PayrollEventActivity
{
    public GarnishmentOrderCompletedEvent(IStringLocalizer<GarnishmentOrderCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(GarnishmentOrderCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Garnishment Order Completed"];
}
