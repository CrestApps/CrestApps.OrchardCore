using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Workflows;

internal abstract class PayrollEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Payroll";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class PayRunOpenedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunOpenedEvent);

    protected override string DisplayText => "Pay Run Opened";

    protected override string Description => "Starts when a pay run is added. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunCalculatedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunCalculatedEvent);

    protected override string DisplayText => "Pay Run Calculated";

    protected override string Description => "Starts when a pay run is calculated. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunSubmittedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunSubmittedEvent);

    protected override string DisplayText => "Pay Run Submitted for Approval";

    protected override string Description => "Starts when a pay run is submitted for approval. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunApprovedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunApprovedEvent);

    protected override string DisplayText => "Pay Run Approved";

    protected override string Description => "Starts when a pay run is approved. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunRejectedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunRejectedEvent);

    protected override string DisplayText => "Pay Run Rejected";

    protected override string Description => "Starts when a pay run's approval is rejected; Note is the approver's. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunProcessedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunProcessedEvent);

    protected override string DisplayText => "Pay Run Processed";

    protected override string Description => "Starts when a pay run is processed, paid in full or not; Status is Paid, PartiallyPaid or Failed. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunPaidEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunPaidEvent);

    protected override string DisplayText => "Pay Run Paid";

    protected override string Description => "Starts when every statement of a processed pay run is paid. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayRunFailedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunFailedEvent);

    protected override string DisplayText => "Pay Run Failed";

    protected override string Description => "Starts when a processed pay run could pay nothing. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal sealed class PayStatementIssuedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayStatementIssuedEvent);

    protected override string DisplayText => "Pay Statement Issued";

    protected override string Description => "Starts when a pay statement becomes final and its payments are made. Input: StatementId, Number, RunId, StaffMemberId, EmployerId, PayDate, Net, Currency, Status, ReversalId and Change.";
}

internal sealed class PayStatementVoidedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayStatementVoidedEvent);

    protected override string DisplayText => "Pay Statement Voided";

    protected override string Description => "Starts when a pay statement is voided; ReversalId is the reversing statement. Input: StatementId, Number, RunId, StaffMemberId, EmployerId, PayDate, Net, Currency, Status, ReversalId and Change.";
}

internal sealed class PayProfileChangedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayProfileChangedEvent);

    protected override string DisplayText => "Pay Profile Changed";

    protected override string Description => "Starts when a pay profile changes, such as its tax id, a tax election or where net pay goes; Change says what. Input: ProfileId, StaffMemberId, EmployerId, Change, Actor and Reason.";
}

internal sealed class PayRunCanceledEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayRunCanceledEvent);

    protected override string DisplayText => "Pay Run Canceled";

    protected override string Description => "Starts when a pay run is cancelled; Note is the reason. Input: PayRunId, Number, EmployerId, PayScheduleId, Type, PeriodStart, PeriodEnd, PayDate, Status, Statements, Change, Actor and Note.";
}

internal static class PayrollFilingEventInputs
{
    public const string Filing = " Input: FilingId, EmployerId, Form, Year, Quarter, Period, Status, FiledOn, Reference, AmendsId, People, Change and Actor.";

    public const string Year = " Input: EmployerId, Year, Change and Actor.";

    public const string Deposit = " Input: DepositId, EmployerId, Jurisdiction, Group, PeriodStart, PeriodEnd, Amount, PaidOn, Reference and RecordedBy.";

    public const string Order = " Input: OrderId, EmployerId, StaffMemberId, ProfileId, Type, CaseNumber, Agency, Status, TotalOwed, PayeeName, PaymentMethod and StartDate.";

    public const string Remittance = " Input: OrderId, EmployerId, StaffMemberId, Type, CaseNumber, Agency, PayeeName, Status, Withheld, TotalOwed, Remaining, StatementId, StatementNumber, PayDate, Amount, Currency, Method, ReferenceId, Reference, Problem and Change.";
}

internal sealed class PayrollFilingPreparedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayrollFilingPreparedEvent);

    protected override string DisplayText => "Payroll Filing Prepared";

    protected override string Description => "Starts when a tax filing draft is prepared, or prepared again." + PayrollFilingEventInputs.Filing;
}

internal sealed class PayrollFilingFiledEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayrollFilingFiledEvent);

    protected override string DisplayText => "Payroll Filing Filed";

    protected override string Description => "Starts when a tax filing is marked as filed." + PayrollFilingEventInputs.Filing;
}

internal sealed class PayrollFilingAmendedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayrollFilingAmendedEvent);

    protected override string DisplayText => "Payroll Filing Amended";

    protected override string Description => "Starts when an amendment of a filed tax filing is started; AmendsId is the filing it corrects." + PayrollFilingEventInputs.Filing;
}

internal sealed class PayrollYearClosedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayrollYearClosedEvent);

    protected override string DisplayText => "Payroll Year Closed";

    protected override string Description => "Starts when an employer's payroll year is closed." + PayrollFilingEventInputs.Year;
}

internal sealed class PayrollYearReopenedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(PayrollYearReopenedEvent);

    protected override string DisplayText => "Payroll Year Reopened";

    protected override string Description => "Starts when an employer's closed payroll year is reopened." + PayrollFilingEventInputs.Year;
}

internal sealed class TaxDepositRecordedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(TaxDepositRecordedEvent);

    protected override string DisplayText => "Tax Deposit Recorded";

    protected override string Description => "Starts when a tax deposit is recorded." + PayrollFilingEventInputs.Deposit;
}

internal sealed class GarnishmentOrderCreatedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(GarnishmentOrderCreatedEvent);

    protected override string DisplayText => "Garnishment Order Created";

    protected override string Description => "Starts when a garnishment order is added." + PayrollFilingEventInputs.Order;
}

internal sealed class GarnishmentWithheldEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(GarnishmentWithheldEvent);

    protected override string DisplayText => "Garnishment Withheld";

    protected override string Description => "Starts when a garnishment order withholds from a pay statement and sends it to the payee; Problem says when the payment could not be made." + PayrollFilingEventInputs.Remittance;
}

internal sealed class GarnishmentRemittanceVoidedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(GarnishmentRemittanceVoidedEvent);

    protected override string DisplayText => "Garnishment Remittance Voided";

    protected override string Description => "Starts when a garnishment remittance is voided with its pay statement and the payee's payment is stopped." + PayrollFilingEventInputs.Remittance;
}

internal sealed class GarnishmentOrderCompletedEventSchema : PayrollEventSchemaBase
{
    public override string Name => nameof(GarnishmentOrderCompletedEvent);

    protected override string DisplayText => "Garnishment Order Completed";

    protected override string Description => "Starts when a garnishment order owes nothing more and is completed." + PayrollFilingEventInputs.Remittance;
}
