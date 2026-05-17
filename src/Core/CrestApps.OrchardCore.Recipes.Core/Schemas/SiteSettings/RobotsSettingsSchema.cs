using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for robots settings.
/// </summary>
public sealed class RobotsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "RobotsSettings";

    /// <summary>
    /// Builds the schema for robots settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the robots.txt file generation.")
            .Properties(
                ("AllowAllAgents", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow all user agents.").Default(true)),
                ("DisallowAdmin", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disallow crawling the admin area.").Default(true)),
                ("AdditionalRules", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Additional custom rules appended to the robots.txt file.")))
            .AdditionalProperties(false);
}
