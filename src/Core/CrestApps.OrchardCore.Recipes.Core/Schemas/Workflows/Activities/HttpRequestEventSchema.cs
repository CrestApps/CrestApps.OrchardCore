using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpRequestEvent</c> workflow event.
/// </summary>
public sealed class HttpRequestEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpRequestEvent";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Request Event";

    /// <inheritdoc />
    protected override string Description => "Triggers a workflow when a specific HTTP request is received on a generated URL";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("HttpMethod", WorkflowActivitySchemaBuilders.String("The HTTP method to match, for example GET or POST."));
        yield return ("Url", WorkflowActivitySchemaBuilders.String("The generated URL path that triggers this event. Managed automatically by the workflow engine; normally omitted from recipes."));
        yield return ("ValidateAntiforgeryToken", WorkflowActivitySchemaBuilders.Boolean("When true, validates the anti-forgery token on the incoming request. Set to false for webhook callers that do not include the token. Defaults to true."));
        yield return ("TokenLifeSpan", WorkflowActivitySchemaBuilders.Integer("The number of days before the generated URL token expires. Use 0 for the token to never expire. Defaults to 0."));
        yield return ("FormLocationKey", WorkflowActivitySchemaBuilders.String("The key used to store and retrieve the current form's location in the workflow output. Leave blank when the workflow does not handle a form or handles only a single one. Defaults to an empty string."));
    }
}
