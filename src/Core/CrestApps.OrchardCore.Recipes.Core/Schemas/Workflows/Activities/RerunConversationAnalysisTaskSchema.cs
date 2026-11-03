using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>RerunConversationAnalysisTask</c> workflow activity.
/// </summary>
public sealed class RerunConversationAnalysisTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "RerunConversationAnalysisTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Rerun AI Conversation Analysis";

    /// <inheritdoc />
    protected override string Description => "Runs a finished AI conversation's post-session tasks and conversion goals again";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Nothing to analyze", "Not found"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SessionId", WorkflowActivitySchemaBuilders.LiquidExpression("The AI session to analyze again. Defaults to {{ Workflow.Input.SessionId }}."));
    }
}
