using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AICompletionWithConfigTask</c> workflow task.
/// </summary>
public sealed class AICompletionWithConfigTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AICompletionWithConfigTask";

    /// <inheritdoc />
    protected override string Category => "Artificial Intelligence";

    /// <inheritdoc />
    protected override string DisplayText => "AI Completion using Direct Config";

    /// <inheritdoc />
    protected override string Description => "Performs AI completion using explicit deployment and configuration parameters";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Drew Blank", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["DeploymentName", "PromptTemplate", "ResultPropertyName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("DeploymentName", WorkflowActivitySchemaBuilders.String("The name or identifier of the chat deployment that should run this workflow task."));
        yield return ("PromptTemplate", WorkflowActivitySchemaBuilders.String("The template used to generate the prompt for the AI model. Supports Liquid syntax."));
        yield return ("SystemMessage", WorkflowActivitySchemaBuilders.String("The system instruction that sets the AI's behavior and response style for the conversation."));
        yield return ("FrequencyPenalty", WorkflowActivitySchemaBuilders.Number("Reduces the chance of repeating tokens proportionally based on how often they have appeared in the text so far. Value between 0 and 1."));
        yield return ("PresencePenalty", WorkflowActivitySchemaBuilders.Number("Reduces the chance of repeating any token that has appeared so far, increasing the likelihood of introducing new topics. Value between 0 and 1."));
        yield return ("Temperature", WorkflowActivitySchemaBuilders.Number("Controls randomness in responses. Lower values produce more deterministic output; higher values produce more creative responses. Value between 0 and 1."));
        yield return ("TopP", WorkflowActivitySchemaBuilders.Number("Controls randomness via nucleus sampling. Lower values narrow token selection to higher-likelihood tokens. Value between 0 and 1."));
        yield return ("MaxTokens", WorkflowActivitySchemaBuilders.Integer("The maximum number of tokens allowed in the model response. One token is roughly 4 characters for typical English text. Minimum 4."));
        yield return ("ResultPropertyName", WorkflowActivitySchemaBuilders.String("The workflow output property name where the AI response will be stored. Prefix with 'AI-' to avoid conflicts with other workflow properties."));
        yield return ("ToolNames", WorkflowActivitySchemaBuilders.StringArray("The names of the AI tools to enable for this task."));
    }
}
