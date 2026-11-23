using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// The base of the mailing list member event schemas.
/// </summary>
internal abstract class MailingListMemberEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Mailing Lists";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
