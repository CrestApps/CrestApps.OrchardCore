using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for user avatar options.
/// </summary>
public sealed class UserAvatarOptionsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "UserAvatarOptions";

    /// <summary>
    /// Builds the schema for user avatar options.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for user avatar behavior.")
            .Properties(
                ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether users are required to upload an avatar.")),
                ("UseDefaultStyle", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the default avatar styling.").Default(true)))
            .AdditionalProperties(false);
}
