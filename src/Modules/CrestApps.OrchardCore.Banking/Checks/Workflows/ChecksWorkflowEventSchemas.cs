using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// The base of the Checks workflow event schemas.
/// </summary>
internal abstract class CheckEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = " Input: CheckId, Number, BankAccountId, PayeeName, Amount, Currency, CheckDate, SourceKind, SourceId, Status, Change, ReplacedById, Reason, Actor.";

    protected override string Category => "Checks";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
