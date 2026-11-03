using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SimulateConversationTask</c> workflow activity.
/// </summary>
public sealed class SimulateConversationTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SimulateConversationTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Simulate AI Conversation";

    /// <inheritdoc />
    protected override string Description => "Tests an AI profile against a customer played by a model, then scores the conversation against criteria";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Passed", "Failed", "Error"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ProfileId", "Scenario"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("The identifier of the AI profile under test."));
        yield return ("Scenario", WorkflowActivitySchemaBuilders.LiquidExpression("Who the simulated customer is, what they want and how they behave."));
        yield return ("Criteria", WorkflowActivitySchemaBuilders.LiquidExpression("The conditions the conversation must meet, one per line."));
        yield return ("Variables", WorkflowActivitySchemaBuilders.LiquidExpression("The conversation variables the test starts with, one name: value per line."));
        yield return ("MaxTurns", WorkflowActivitySchemaBuilders.Integer("The most customer turns, from 1 to 50. Defaults to 8."));
        yield return ("PassingScore", WorkflowActivitySchemaBuilders.Integer("The lowest score out of 100 that passes. Defaults to 70."));
        yield return ("Label", WorkflowActivitySchemaBuilders.LiquidExpression("Optionally, a label that groups the results, such as the test suite."));
        yield return ("CustomerDeploymentName", WorkflowActivitySchemaBuilders.String("Optionally, the deployment that plays the customer. Defaults to the utility deployment."));
        yield return ("JudgeDeploymentName", WorkflowActivitySchemaBuilders.String("Optionally, the deployment that judges the conversation. Defaults to the utility deployment."));
    }
}
