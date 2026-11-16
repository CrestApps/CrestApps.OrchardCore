using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// The base of the Approvals workflow event schemas.
/// </summary>
internal abstract class ApprovalEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = " Input: RequestId, SubjectKind, SubjectId, Title, PolicyId, PolicyTitle, Status, Change, RequesterName, ApproverUserNames (comma separated, for Notify User Task), Decider, Note, DueUtc, Stage, Amount and Currency.";

    protected override string Category => "Approvals";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [("ApprovalType", WorkflowActivitySchemaBuilders.String("Only requests of this approval type, such as PayRun or Ticket; empty for every type."))];
}
