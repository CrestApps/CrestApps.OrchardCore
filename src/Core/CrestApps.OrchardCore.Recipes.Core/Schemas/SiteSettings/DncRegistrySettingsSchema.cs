using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for DNC registry import enforcement settings.
/// </summary>
public sealed class DncRegistrySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "DncRegistrySettings";

    /// <summary>
    /// Builds the schema for DNC registry import enforcement settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for globally enforcing do-not-call registry checks during content imports.")
            .Properties(
                ("EnforceGlobally", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether do-not-call registry checks are globally enforced for content imports.")),
                ("EnforcedRegistryKeys", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("The registry keys that are always checked during content imports.")))
            .AdditionalProperties(false);
}
