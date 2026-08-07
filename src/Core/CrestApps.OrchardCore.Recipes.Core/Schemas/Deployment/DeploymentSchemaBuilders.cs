using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;

/// <summary>
/// Provides reusable JSON schema fragments for describing deployment step payload properties.
/// </summary>
public static class DeploymentSchemaBuilders
{
    /// <summary>
    /// Creates a schema for a boolean property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description(description);

    /// <summary>
    /// Creates a schema for a string property.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder String(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description(description);

    /// <summary>
    /// Creates a schema for an array of strings.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder StringArray(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description(description);

    /// <summary>
    /// Creates a schema for an array of strings that surfaces the provided values as non-restrictive
    /// suggestions on each item while still allowing any custom value.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="itemExamples">The well-known values to surface as suggestions on each array item.</param>
    public static JsonSchemaBuilder StringArray(string description, IEnumerable<string> itemExamples)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .WithSuggestions(itemExamples))
            .Description(description);

    /// <summary>
    /// Creates a schema for the shared <c>IncludeAll</c> flag used by many deployment steps.
    /// </summary>
    /// <param name="itemsDescription">A short noun phrase naming what is exported when the flag is set.</param>
    public static JsonSchemaBuilder IncludeAll(string itemsDescription)
        => Boolean($"When true, exports all {itemsDescription} and the accompanying selection array is ignored. When false, only the named entries are exported.");
}
