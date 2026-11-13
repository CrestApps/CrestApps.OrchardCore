using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Workflows;

internal sealed class BankAccountChangedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => BankAccountChangedEvent.EventName;

    protected override string Category => "Banking";

    protected override string DisplayText => "Bank Account Changed";

    protected override string Description => "Starts when a bank account is added, its number or status changes, or it is removed. Input: BankAccountId, Name, OwnerKind, OwnerId, Status, PreviousStatus, Changes (Created, Updated, NumberChanged, StatusChanged, Deleted), Actor. It never holds a number.";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
