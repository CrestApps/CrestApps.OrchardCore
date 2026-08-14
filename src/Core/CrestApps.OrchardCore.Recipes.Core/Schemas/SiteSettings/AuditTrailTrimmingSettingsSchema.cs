using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for audit trail trimming settings.
/// </summary>
public sealed class AuditTrailTrimmingSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AuditTrailTrimmingSettings";

    /// <summary>
    /// Builds the schema for audit trail trimming settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for automatic trimming of old audit trail entries.")
            .Properties(
                ("RetentionDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The number of days to retain audit trail entries before trimming.").Default(10)),
                ("Disabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether automatic trimming is disabled.")))
            .AdditionalProperties(false);
}
