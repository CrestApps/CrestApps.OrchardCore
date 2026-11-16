using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// The base of the Disbursements workflow event schemas.
/// </summary>
internal abstract class DisbursementEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = " Input: DisbursementId, PayeeName, Amount, Currency, Method, Status, Change, SourceKind, SourceId, BatchId, TraceNumber, FailureCode, FailureReason, IsPrenote and Actor.";

    protected override string Category => "Disbursements";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
