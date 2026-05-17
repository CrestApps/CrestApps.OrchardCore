using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for content audit trail settings.
/// </summary>
public sealed class ContentAuditTrailSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ContentAuditTrailSettings";

    /// <summary>
    /// Builds the schema for content audit trail settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for content-specific audit trail tracking.")
            .Properties(
                ("AllowedContentTypes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The content types to track in the audit trail.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(false);
}
