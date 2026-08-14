using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>AICompletionFromProfileTask</c> workflow task.
/// </summary>
public sealed class AICompletionFromProfileTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AICompletionFromProfileTask";

    /// <inheritdoc />
    protected override string Category => "Artificial Intelligence";

    /// <inheritdoc />
    protected override string DisplayText => "AI Completion using Profile";

    /// <inheritdoc />
    protected override string Description => "Performs AI completion using a configured AI profile";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Drew Blank", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ProfileId", "PromptTemplate", "ResultPropertyName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ProfileId", WorkflowActivitySchemaBuilders.String("The identifier of the AI profile to use when generating the response."));
        yield return ("PromptTemplate", WorkflowActivitySchemaBuilders.String("The template used to generate the prompt for the AI model. Supports Liquid syntax."));
        yield return ("ResultPropertyName", WorkflowActivitySchemaBuilders.String("The workflow output property name where the AI response will be stored. Prefix with 'AI-' to avoid conflicts with other workflow properties."));
    }
}
