using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Describes the Unsubscribe from Mailing List task.
/// </summary>
internal sealed class UnsubscribeFromMailingListTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "UnsubscribeFromMailingListTask";

    protected override string Category => "Mailing Lists";

    protected override string DisplayText => "Unsubscribe from Mailing List";

    protected override string Description => "Unsubscribes an address from a mailing list, or from every list";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["Email"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("List", WorkflowActivitySchemaBuilders.String("The mailing list's id or technical name; empty for every list. A plain string that supports Liquid."));
        yield return ("Email", WorkflowActivitySchemaBuilders.String("The e-mail address. A plain string that supports Liquid."));
    }
}
