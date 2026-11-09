using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.ContactCenter.Recipes.Workflows;

/// <summary>
/// Describes the recipe schema of the <c>FindAgentNumbersTask</c> workflow task.
/// </summary>
public sealed class FindAgentNumbersTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(FindAgentNumbersTask);

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Find Agent Numbers";

    /// <inheritdoc />
    protected override string Description => $"Finds the phone and SMS numbers an agent calls and texts from, falling back to the default numbers of the Contact Center settings, and writes them to the workflow output as '{FindAgentNumbersTask.PhoneNumberOutput}' and '{FindAgentNumbersTask.SmsNumberOutput}'";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "NotFound"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(FindAgentNumbersTask.UserName), WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the agent's user name, user id or email. When it is empty or names no user, the default numbers are used."));
    }
}
