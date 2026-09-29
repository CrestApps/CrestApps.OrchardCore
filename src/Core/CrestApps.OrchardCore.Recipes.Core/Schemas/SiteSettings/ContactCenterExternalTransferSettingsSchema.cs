using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Contact Center external transfer settings.
/// </summary>
public sealed class ContactCenterExternalTransferSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ContactCenterExternalTransferSettings";

    /// <summary>
    /// Builds the schema for the external transfer settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The operator-curated catalog of approved external transfer destinations.")
            .Properties(
                ("Destinations", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Opaque stable identifier callers supply when requesting a transfer to this destination.")),
                            ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Operator-assigned display name of the destination.")),
                            ("E164Address", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Canonical E.164 address the call is transferred to.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the destination is active. Disabled destinations are always denied.")))
                        .AdditionalProperties(false))
                    .Description("Approved external destinations. Only entries that are present and enabled are reachable by an external transfer.")),
                ("AllowUnlistedNumbers", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an agent who may transfer externally may also dial a number that is not in 'Destinations'. The number must still pass the platform's dial policy.")))
            .AdditionalProperties(false);
}
