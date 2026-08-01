using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UpdateTwitterStatusTask</c> workflow task.
/// </summary>
public sealed class UpdateTwitterStatusTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UpdateTwitterStatusTask";

    /// <inheritdoc />
    protected override string Category => "Social";

    /// <inheritdoc />
    protected override string DisplayText => "Update X (Twitter) Status Task";

    /// <inheritdoc />
    protected override string Description => "Posts a status update to X (Twitter) using the configured application credentials";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["StatusTemplate"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("StatusTemplate", WorkflowActivitySchemaBuilders.LiquidExpression("The status text of the post."));
    }
}
