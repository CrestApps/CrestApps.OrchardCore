using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Canada DNCL registry settings.
/// </summary>
public sealed class CanadaDnclRegistrySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "CanadaDnclRegistrySettings";

    /// <summary>
    /// Builds the schema for Canada DNCL registry settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Canada LNNTE-DNCL Registry provider.")
            .Properties(
                ("ProtectedApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encrypted API key used to authenticate with the Canada LNNTE-DNCL Registry.")),
                ("AccountNumber", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Canada LNNTE-DNCL account number used for registry lookups.")),
                ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The base URL for the Canada LNNTE-DNCL Registry API.")))
            .AdditionalProperties(false);
}
