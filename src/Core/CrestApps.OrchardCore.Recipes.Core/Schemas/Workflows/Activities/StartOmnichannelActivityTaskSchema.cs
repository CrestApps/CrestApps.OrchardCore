using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>StartOmnichannelActivityTask</c> workflow task.
/// </summary>
public sealed class StartOmnichannelActivityTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StartOmnichannelActivityTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Place Call or Send Message";

    /// <inheritdoc />
    protected override string Description => "Starts an automated omnichannel activity now: places the outbound call for a Phone activity, or sends the opening message for an SMS activity. The activity's own channel decides which.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Already Started", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ActivityItemId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the CRM activity identifier to start."));
    }
}
