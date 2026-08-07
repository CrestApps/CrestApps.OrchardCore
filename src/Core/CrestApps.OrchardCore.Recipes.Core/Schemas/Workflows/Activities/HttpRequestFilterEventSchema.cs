using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpRequestFilterEvent</c> workflow event.
/// </summary>
public sealed class HttpRequestFilterEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpRequestFilterEvent";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Request Filter Event";

    /// <inheritdoc />
    protected override string Description => "Triggers inside an active request handler when the current HTTP request matches the configured route values";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Matched"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("HttpMethod", WorkflowActivitySchemaBuilders.String("The HTTP method to match, for example GET or POST."));
        yield return ("RouteValues", new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("The route values to match. The controller, action, and area keys map to the ControllerName, ActionName, and AreaName properties respectively. Omit or leave empty to match any route."));
    }
}
