using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Approvals.Workflows;

internal abstract class ApprovalTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Liquid = " A plain string that supports Liquid.";

    protected override string Category => "Approvals";
}

internal sealed class RequestApprovalTaskSchema : ApprovalTaskSchemaBase
{
    public override string Name => nameof(RequestApprovalTask);

    protected override string DisplayText => "Request Approval";

    protected override string Description => "Asks for an approval of a type, under the policy named or the first enabled one whose type, amount and scope match. Asking again while a request about the subject is pending gives that request. Writes the request id to the workflow output ApprovalRequestId.";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Policy", WorkflowActivitySchemaBuilders.String("The approval policy, by id or technical name; empty to use the first enabled policy whose type, amount and scope match." + Liquid));
        yield return ("SubjectKind", WorkflowActivitySchemaBuilders.String("The approval type, such as PayRun or Ticket." + Liquid));
        yield return ("SubjectId", WorkflowActivitySchemaBuilders.String("The id of the subject." + Liquid));
        yield return ("AccessScope", WorkflowActivitySchemaBuilders.String("The access scope within the subject kind, such as an employer." + Liquid));
        yield return ("Title", WorkflowActivitySchemaBuilders.String("What is asked." + Liquid));
        yield return ("SubjectUrl", WorkflowActivitySchemaBuilders.String("The subject's page, application-relative such as ~/Admin/..." + Liquid));
        yield return ("Amount", WorkflowActivitySchemaBuilders.String("The amount, a number with a dot, that policy conditions match." + Liquid));
        yield return ("Currency", WorkflowActivitySchemaBuilders.String("The currency of the amount." + Liquid));
    }
}

internal sealed class ApproveRequestTaskSchema : ApprovalTaskSchemaBase
{
    public override string Name => nameof(ApproveRequestTask);

    protected override string DisplayText => "Approve Request";

    protected override string Description => "Approves a request as the person the workflow runs for, under the request's policy and the two built-in rules.";

    protected override IEnumerable<string> Outcomes => ["Done", "NotAllowed", "NotFound"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequestId", WorkflowActivitySchemaBuilders.String("The request's id." + Liquid));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("The approver's note." + Liquid));
    }
}

internal sealed class RejectRequestTaskSchema : ApprovalTaskSchemaBase
{
    public override string Name => nameof(RejectRequestTask);

    protected override string DisplayText => "Reject Request";

    protected override string Description => "Rejects a request as the person the workflow runs for, under the request's policy and the two built-in rules. The note is required.";

    protected override IEnumerable<string> Outcomes => ["Done", "NotAllowed", "NotFound"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequestId", WorkflowActivitySchemaBuilders.String("The request's id." + Liquid));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("Why it is rejected." + Liquid));
    }
}

internal sealed class GetApprovalRequestTaskSchema : ApprovalTaskSchemaBase
{
    public override string Name => nameof(GetApprovalRequestTask);

    protected override string DisplayText => "Get Approval Request";

    protected override string Description => "Reads where a request is, such as before a reminder. Empty RequestId reads the workflow's correlation id. Writes ApprovalRequestStatus and ApprovalCount to the workflow output.";

    protected override IEnumerable<string> Outcomes => ["Pending", "Approved", "Rejected", "Canceled", "NotFound"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequestId", WorkflowActivitySchemaBuilders.String("The request's id; empty for the workflow's correlation id." + Liquid));
    }
}
