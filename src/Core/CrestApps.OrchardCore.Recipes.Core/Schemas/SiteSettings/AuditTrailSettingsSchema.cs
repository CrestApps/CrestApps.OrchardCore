using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for audit trail settings.
/// </summary>
public sealed class AuditTrailSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AuditTrailSettings";

    /// <summary>
    /// Builds the schema for audit trail settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for audit trail event recording.")
            .Properties(
                ("Categories", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The list of audit trail event category configurations.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The category name.")), ("Events", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The events in this category.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The event name.")), ("Category", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The event category.")), ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether this event is enabled for recording."))).AdditionalProperties(false)))).AdditionalProperties(false))),
                ("ClientIpAddressAllowed", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to record the client IP address in audit trail entries.")))
            .AdditionalProperties(false);
}
