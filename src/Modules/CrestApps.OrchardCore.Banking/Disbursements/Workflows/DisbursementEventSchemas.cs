using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// The base of the Disbursements workflow event schemas.
/// </summary>
internal abstract class DisbursementEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = " Input: DisbursementId, PayeeName, Amount, Currency, Method, Status, Change, SourceKind, SourceId, BatchId, TraceNumber, FailureCode, FailureReason, IsPrenote and Actor.";

    protected const string BatchInput = " Input: BatchId, Provider, SourceBankAccountId, SourceBankAccountName, EffectiveDate, Count, TotalAmount, Currency, Status, PreviousStatus, SourceKind, AccessScope, FileName, ProviderReference, Change, Reason, Settled and Actor.";

    protected const string ReturnsInput = " Input: Returned, Problems (how many lines could not be applied), ProblemMessages and Actor.";

    protected override string Category => "Disbursements";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
