using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for general AI settings.
/// </summary>
public sealed class GeneralAISettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "GeneralAISettings";

    /// <summary>
    /// Builds the schema for general AI settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("General configuration for AI services.")
            .Properties(
                ("EnableAIUsageTracking", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to track AI usage metrics.")),
                ("EnableAnalytics", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable AI analytics.")),
                ("EnablePreemptiveMemoryRetrieval", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable preemptive memory retrieval for AI sessions.")),
                ("OverrideMaximumIterationsPerRequest", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to override the default maximum iterations per request.")),
                ("MaximumIterationsPerRequest", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of tool-call iterations allowed per AI request.")),
                ("OverrideEnableDistributedCaching", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to override the distributed caching setting.")),
                ("EnableDistributedCaching", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable distributed caching for AI responses.")),
                ("OverrideEnableOpenTelemetry", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to override the OpenTelemetry setting.")),
                ("EnableOpenTelemetry", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable OpenTelemetry tracing for AI operations.")))
            .AdditionalProperties(false);
}
