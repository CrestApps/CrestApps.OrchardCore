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
