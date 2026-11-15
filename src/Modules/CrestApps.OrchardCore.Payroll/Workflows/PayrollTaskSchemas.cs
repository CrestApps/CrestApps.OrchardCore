using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Workflows;

internal abstract class PayrollTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Liquid = " A plain string that supports Liquid.";

    protected override string Category => "Payroll";
}

internal sealed class OpenPayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(OpenPayRunTask);

    protected override string DisplayText => "Open Pay Run";

    protected override string Description => "Adds a pay run for an employer and a pay schedule, for the schedule's period of today unless a period is given. A run of the same employer, schedule, type and period that is not cancelled is not added twice. Writes PayRunId and PayRunNumber to the output.";

    protected override IEnumerable<string> Outcomes => ["Done", "Exists", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("EmployerId", WorkflowActivitySchemaBuilders.String("The employer's id." + Liquid));
        yield return ("PayScheduleId", WorkflowActivitySchemaBuilders.String("The pay schedule's id." + Liquid));
        yield return ("RunType", WorkflowActivitySchemaBuilders.String("Regular, Supplemental, OffCycle, Final, Correction or Contractor; Regular when empty." + Liquid));
        yield return ("PeriodStart", WorkflowActivitySchemaBuilders.String("yyyy-MM-dd, or empty for the schedule's period of today." + Liquid));
        yield return ("PeriodEnd", WorkflowActivitySchemaBuilders.String("yyyy-MM-dd, or empty." + Liquid));
        yield return ("PayDate", WorkflowActivitySchemaBuilders.String("yyyy-MM-dd, or empty for the schedule's." + Liquid));
    }
}

internal sealed class AddPayInputTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(AddPayInputTask);

    protected override string DisplayText => "Add Pay Input";

    protected override string Description => "Adds hours or an amount to an open pay run for one person, by employee number and pay code, as a file row would. The run is calculated again for that person when it was calculated.";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
        yield return ("EmployeeNumber", WorkflowActivitySchemaBuilders.String("The person's employee number." + Liquid));
        yield return ("PayCode", WorkflowActivitySchemaBuilders.String("The pay code, such as BONUS." + Liquid));
        yield return ("Hours", WorkflowActivitySchemaBuilders.String("Hours, with a dot for decimals." + Liquid));
        yield return ("Amount", WorkflowActivitySchemaBuilders.String("An amount, with a dot for decimals." + Liquid));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("Why." + Liquid));
    }
}

internal sealed class CalculatePayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(CalculatePayRunTask);

    protected override string DisplayText => "Calculate Pay Run";

    protected override string Description => "Calculates a pay run. Ends in Blocked when a person has a blocker. Writes Statements, Gross, Net and CashNeeded to the output.";

    protected override IEnumerable<string> Outcomes => ["Done", "Blocked", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
    }
}

internal sealed class SubmitPayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(SubmitPayRunTask);

    protected override string DisplayText => "Submit Pay Run for Approval";

    protected override string Description => "Submits a calculated pay run under the Pay run approval policy. Writes ApprovalRequestId to the output.";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
    }
}

internal sealed class ApprovePayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(ApprovePayRunTask);

    protected override string DisplayText => "Approve Pay Run";

    protected override string Description => "Approves a pay run's pending approval as the person the workflow runs for, under the policy: a workflow with no user, or the person who prepared the run, is not allowed.";

    protected override IEnumerable<string> Outcomes => ["Done", "NotAllowed", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("The approver's note." + Liquid));
    }
}

internal sealed class RejectPayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(RejectPayRunTask);

    protected override string DisplayText => "Reject Pay Run";

    protected override string Description => "Rejects a pay run's pending approval as the person the workflow runs for, under the policy. The note is required.";

    protected override IEnumerable<string> Outcomes => ["Done", "NotAllowed", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("Why it is rejected." + Liquid));
    }
}

internal sealed class ProcessPayRunTaskSchema : PayrollTaskSchemaBase
{
    public override string Name => nameof(ProcessPayRunTask);

    protected override string DisplayText => "Process Pay Run";

    protected override string Description => "Processes an approved pay run, or pays what is left of a partly paid one. Ends in Paid, PartiallyPaid or Failed.";

    protected override IEnumerable<string> Outcomes => ["Paid", "PartiallyPaid", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PayRunId", WorkflowActivitySchemaBuilders.String("The run's id or number." + Liquid));
    }
}
