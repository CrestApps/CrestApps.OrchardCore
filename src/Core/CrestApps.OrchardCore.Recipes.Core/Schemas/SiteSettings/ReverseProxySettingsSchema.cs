using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for reverse proxy settings.
/// </summary>
public sealed class ReverseProxySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ReverseProxySettings";

    /// <summary>
    /// Builds the schema for reverse proxy settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for reverse proxy header forwarding.")
            .Properties(
                ("ForwardedHeaders", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The forwarded headers to process.").Enum("None", "XForwardedFor", "XForwardedHost", "XForwardedProto", "All")),
                ("KnownNetworks", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The list of known network addresses.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("KnownProxies", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The list of known proxy IP addresses.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(false);
}
