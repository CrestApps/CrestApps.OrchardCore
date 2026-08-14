using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for AI memory settings.
/// </summary>
public sealed class AIMemorySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AIMemorySettings";

    /// <summary>
    /// Builds the schema for AI memory settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for AI memory storage and retrieval.")
            .Properties(
                ("IndexProfileName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the index profile used for memory storage.")),
                ("TopN", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of memory entries to retrieve per query.").Default(5)))
            .AdditionalProperties(false);
}
