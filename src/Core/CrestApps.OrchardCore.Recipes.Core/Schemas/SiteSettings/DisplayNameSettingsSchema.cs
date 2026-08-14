using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for display name settings.
/// </summary>
public sealed class DisplayNameSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "DisplayNameSettings";

    /// <summary>
    /// Builds the schema for display name settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for how user display names are generated and which name fields are required.")
            .Properties(
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Username", "FirstThenLast", "LastThenFirst", "DisplayName", "Other").Description("The display name generation strategy.")),
                ("Template", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A Liquid template for generating custom display names when Type is 'Other'.")),
                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Optional", "Required").Description("Whether the display name field is hidden, optional, or required.")),
                ("FirstName", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Optional", "Required").Description("Whether the first name field is hidden, optional, or required.")),
                ("LastName", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Optional", "Required").Description("Whether the last name field is hidden, optional, or required.")),
                ("MiddleName", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Optional", "Required").Description("Whether the middle name field is hidden, optional, or required.")))
            .AdditionalProperties(false);
}
