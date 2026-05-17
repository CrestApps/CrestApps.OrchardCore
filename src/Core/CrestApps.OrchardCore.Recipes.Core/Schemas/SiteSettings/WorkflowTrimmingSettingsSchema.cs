using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for workflow trimming settings.
/// </summary>
public sealed class WorkflowTrimmingSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "WorkflowTrimmingSettings";

    /// <summary>
    /// Builds the schema for workflow trimming settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for automatic trimming of old workflow instances.")
            .Properties(
                ("RetentionDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The number of days to retain workflow instances before trimming.").Default(90)),
                ("Disabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether automatic workflow trimming is disabled.")),
                ("Statuses", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The workflow statuses to include in trimming.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Idle", "Starting", "Resuming", "Executing", "Halted", "Finished", "Faulted", "Aborted"))))
            .AdditionalProperties(false);
}
