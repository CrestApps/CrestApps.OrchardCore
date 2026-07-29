using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>HttpRedirectToFormLocationTask</c> workflow task.
/// </summary>
public sealed class HttpRedirectToFormLocationTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HttpRedirectToFormLocationTask";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Http Redirect To Form Location Task";

    /// <inheritdoc />
    protected override string Description => "Redirects the HTTP response to the form location stored in the workflow output under the given key";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("FormLocationKey", WorkflowActivitySchemaBuilders.String("This key name should be equal to the 'Form Location Key' of the HTTP request event. Leave blank if the workflow only handles a single form. Defaults to \"\"."));
    }
}
