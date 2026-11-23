using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Describes the Subscribe to Mailing List task.
/// </summary>
internal sealed class SubscribeToMailingListTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "SubscribeToMailingListTask";

    protected override string Category => "Mailing Lists";

    protected override string DisplayText => "Subscribe to Mailing List";

    protected override string Description => "Signs an address up to a mailing list, as the list says";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["List", "Email"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("List", WorkflowActivitySchemaBuilders.String("The mailing list's id or technical name. A plain string that supports Liquid."));
        yield return ("Email", WorkflowActivitySchemaBuilders.String("The e-mail address. A plain string that supports Liquid."));
        yield return ("PersonName", WorkflowActivitySchemaBuilders.String("The person's name, optional. A plain string that supports Liquid."));
    }
}
