using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for USA FTC DNC registry settings.
/// </summary>
public sealed class UsaFtcDncRegistrySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "UsaFtcDncRegistrySettings";

    /// <summary>
    /// Builds the schema for USA FTC DNC registry settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the USA FTC Do Not Call Registry provider.")
            .Properties(
                ("ProtectedApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encrypted API key used to authenticate with the USA FTC Do Not Call Registry.")),
                ("OrganizationId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The USA FTC organization identifier used for registry lookups.")),
                ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The base URL for the USA FTC Do Not Call Registry API.")))
            .AdditionalProperties(false);
}
