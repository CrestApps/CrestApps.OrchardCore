using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for export content to deployment target settings.
/// </summary>
public sealed class ExportContentToDeploymentTargetSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ExportContentToDeploymentTargetSettings";

    /// <summary>
    /// Builds the schema for export content to deployment target settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the export content to deployment target feature.")
            .Properties(
                ("ExportContentToDeploymentTargetPlanId", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The deployment plan ID used for exporting content.")))
            .AdditionalProperties(false);
}
