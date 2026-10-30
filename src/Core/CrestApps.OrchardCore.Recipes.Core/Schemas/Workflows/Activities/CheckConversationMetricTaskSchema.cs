using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CheckConversationMetricTask</c> workflow activity.
/// </summary>
public sealed class CheckConversationMetricTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CheckConversationMetricTask";

    /// <inheritdoc />
    protected override string Category => "AI Conversations";

    /// <inheritdoc />
    protected override string DisplayText => "Check AI Conversation Metric";

    /// <inheritdoc />
    protected override string Description => "Measures an AI conversation metric over the last window and says when it crosses a threshold and when it is back";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Alert started", "Still alerting", "Resolved", "Normal"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Metric", WorkflowActivitySchemaBuilders.EnumValue("The metric to measure. Rates are percentages.", "Conversations", "ResolutionRate", "AverageHandleTimeSeconds", "AverageResponseLatencyMs", "NegativeFeedbackRate", "AverageQAScore", "QAFailureRate", "ToolFailureRate", "HallucinationRate", "AICalls", "AICallHandoffRate", "AICallFailureRate", "AverageTimeToFirstAudioMs"));
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("Optionally measure one AI profile only."));
        yield return ("WindowMinutes", WorkflowActivitySchemaBuilders.Integer("The length of the window, in minutes. Defaults to 60."));
        yield return ("Condition", WorkflowActivitySchemaBuilders.EnumValue("How the value is compared with the threshold.", "Above", "Below", "RisesByPercent", "FallsByPercent"));
        yield return ("Threshold", WorkflowActivitySchemaBuilders.Number("The threshold."));
        yield return ("MinimumSampleSize", WorkflowActivitySchemaBuilders.Integer("The fewest conversations, evaluations or calls the metric must be measured over before it can alert. Defaults to 10."));
    }
}
