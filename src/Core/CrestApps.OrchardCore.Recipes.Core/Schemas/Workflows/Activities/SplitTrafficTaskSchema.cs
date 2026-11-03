using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SplitTrafficTask</c> workflow activity.
/// </summary>
public sealed class SplitTrafficTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SplitTrafficTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Split Traffic";

    /// <inheritdoc />
    protected override string Description => "Sends each run down one of several weighted outcomes, for an A/B test or a sample";

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Variants"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Variants", WorkflowActivitySchemaBuilders.String("The outcomes and their weights, one name: weight per line. Each name is an outcome."));
        yield return ("StickyKey", WorkflowActivitySchemaBuilders.LiquidExpression("Optionally, a key such as the customer's phone number that always takes the same outcome."));
    }
}
