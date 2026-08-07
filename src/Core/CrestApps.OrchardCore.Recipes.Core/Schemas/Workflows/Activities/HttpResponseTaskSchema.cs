using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpResponseTask</c> workflow task.
/// </summary>
public sealed class HttpResponseTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpResponseTask";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Response Task";

    /// <inheritdoc />
    protected override string Description => "Writes an HTTP response with a specified status code, content type, headers, and body";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.LiquidExpression("The response body to send to the client."));
        yield return ("HttpStatusCode", WorkflowActivitySchemaBuilders.Integer("The HTTP status code to return. Defaults to 200."));
        yield return ("Headers", WorkflowActivitySchemaBuilders.LiquidExpression("Additional HTTP response headers, one key-value pair per line, for example 'X-MyHeader: Foo'."));
        yield return ("ContentType", WorkflowActivitySchemaBuilders.LiquidExpression("The content type of the response body. Defaults to application/json."));
    }
}
