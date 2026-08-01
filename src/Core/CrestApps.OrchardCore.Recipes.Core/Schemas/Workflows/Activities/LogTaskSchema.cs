using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>LogTask</c> workflow task.
/// </summary>
public sealed class LogTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "LogTask";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Log Task";

    /// <inheritdoc />
    protected override string Description => "Writes a message to the application log";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("LogLevel", WorkflowActivitySchemaBuilders.EnumValue("The log level used when writing the message. Defaults to 'Information'.", "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"));
        yield return ("Text", WorkflowActivitySchemaBuilders.LiquidExpression("The text to log."));
    }
}
