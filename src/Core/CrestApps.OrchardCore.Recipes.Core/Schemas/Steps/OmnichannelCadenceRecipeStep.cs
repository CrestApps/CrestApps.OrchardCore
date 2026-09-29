using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelCadence" recipe step — imports re-engagement cadences: named, ordered follow-up messages
/// sent to a contact who has gone quiet in an automated conversation.
/// </summary>
public sealed class OmnichannelCadenceRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "OmnichannelCadence";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Cadences", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("cadence")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("Name of the cadence shown when it is selected on a loading campaign.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the cadence.")),
                            ("Steps", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("DelayMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Minutes the contact must be silent since the automation's previous message before this nudge is sent. The clock restarts after each nudge.")),
                                        ("IsAiGenerated", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the AI composes this nudge. When false, 'Message' is sent verbatim.")),
                                        ("Message", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The exact text sent when 'IsAiGenerated' is false (required in that case), or optional guidance the AI composes the nudge from when it is true.")))
                                    .AdditionalProperties(true))
                                .Description("Ordered nudges. The number of steps caps how many nudges are ever sent; an empty list never nudges.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the cadence is enabled. A disabled cadence never nudges.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("cadence")))
                        .Required("DisplayText")
                        .AdditionalProperties(true))
                    .Description("The re-engagement cadences to create or update.")),
            ],
            ["Cadences"]);
}
