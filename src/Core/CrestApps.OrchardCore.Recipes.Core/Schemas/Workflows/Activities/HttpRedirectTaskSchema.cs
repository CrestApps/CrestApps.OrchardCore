using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpRedirectTask</c> workflow task.
/// </summary>
public sealed class HttpRedirectTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpRedirectTask";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Redirect Task";

    /// <inheritdoc />
    protected override string Description => "Redirects the current HTTP request to a specified URL";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Location", WorkflowActivitySchemaBuilders.LiquidExpression("The URL to redirect to."));
        yield return ("Permanent", WorkflowActivitySchemaBuilders.Boolean("When true, sends a 301 Permanent Redirect; otherwise sends a 302 Temporary Redirect. Defaults to false."));
    }
}
