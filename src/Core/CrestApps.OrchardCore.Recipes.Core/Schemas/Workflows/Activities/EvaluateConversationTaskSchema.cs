using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>EvaluateConversationTask</c> workflow activity.
/// </summary>
public sealed class EvaluateConversationTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "EvaluateConversationTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Evaluate AI Conversation";

    /// <inheritdoc />
    protected override string Description => "Scores a finished AI conversation against criteria and keeps the result for QA reports and alerts";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Passed", "Failed", "Not evaluated"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SessionId", WorkflowActivitySchemaBuilders.LiquidExpression("The AI session to evaluate. Defaults to {{ Workflow.Input.SessionId }}."));
        yield return ("Criteria", WorkflowActivitySchemaBuilders.LiquidExpression("The conditions a successful conversation meets, one per line."));
        yield return ("PassingScore", WorkflowActivitySchemaBuilders.Integer("The lowest score out of 100 that passes. Defaults to 70."));
        yield return ("Label", WorkflowActivitySchemaBuilders.LiquidExpression("Optionally, a label that groups evaluations in reports, such as a cohort or a variant."));
    }
}
