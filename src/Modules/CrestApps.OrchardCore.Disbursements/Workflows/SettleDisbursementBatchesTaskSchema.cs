using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Disbursements.Workflows;

internal sealed class SettleDisbursementBatchesTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(SettleDisbursementBatchesTask);

    protected override string DisplayText => "Settle Disbursement Batches";

    protected override string Description => "Records as paid the disbursements of every batch released more than AgeInDays days ago that the bank did not return, such as from a Timer Event once a day. Writes how many settled to the workflow output SettledDisbursements.";

    protected override string Category => "Disbursements";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("AgeInDays", WorkflowActivitySchemaBuilders.Integer($"How many days after its release a batch settles, 1 to 60. Defaults to {SettleDisbursementBatchesTask.DefaultAgeInDays}."));
    }
}
