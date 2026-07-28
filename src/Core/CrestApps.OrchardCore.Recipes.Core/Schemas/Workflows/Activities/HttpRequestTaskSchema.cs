using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpRequestTask</c> workflow task.
/// </summary>
public sealed class HttpRequestTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpRequestTask";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Request Task";

    /// <inheritdoc />
    protected override string Description => "Sends an HTTP request to a specified URL and routes the workflow based on the response status code";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["UnhandledHttpStatus"];

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Url"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Url", WorkflowActivitySchemaBuilders.LiquidExpression("The URL to send the request to."));
        yield return ("HttpMethod", WorkflowActivitySchemaBuilders.String("The HTTP method to use, for example GET, POST, PUT, PATCH, or DELETE. Defaults to GET."));
        yield return ("Headers", WorkflowActivitySchemaBuilders.LiquidExpression("Additional HTTP request headers, one key-value pair per line, for example 'Authorization: Bearer token'."));
        yield return ("Body", WorkflowActivitySchemaBuilders.LiquidExpression("The request body to send with POST, PUT, and PATCH requests."));
        yield return ("ContentType", WorkflowActivitySchemaBuilders.LiquidExpression("The content type of the request body. Defaults to application/json."));
        yield return ("HttpResponseCodes", WorkflowActivitySchemaBuilders.String("A comma-separated list of HTTP response status codes to handle as activity outcomes, for example '200, 404, 500'. Each code becomes an outcome name. Defaults to '200'."));
    }
}
