using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Workflows;

internal sealed class BankAccountStatusChangedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(BankAccountStatusChangedEvent);

    protected override string Category => "Banking";

    protected override string DisplayText => "Bank Account Status Changed";

    protected override string Description => "Starts when a bank account's status changes, such as when it is verified or closed. Input: BankAccountId, Name, OwnerKind, OwnerId, Status, PreviousStatus, Reason, Changes and Actor. It never holds a number.";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [("Status", WorkflowActivitySchemaBuilders.String("Only this new status: Unverified, VerificationSent, Verified, Failed or Closed; empty for every status."))];
}
