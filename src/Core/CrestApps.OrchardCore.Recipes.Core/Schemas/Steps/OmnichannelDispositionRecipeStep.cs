using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelDisposition" recipe step — imports the dispositions that classify how an activity ended.
/// </summary>
public sealed class OmnichannelDispositionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelDisposition";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelDisposition").Description("Recipe step discriminator. Must be 'OmnichannelDisposition'.")),
                ("Dispositions", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("disposition")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Unique name of the disposition.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Legacy display text. Prefer 'Name'; this is only used to seed the name when 'Name' is not supplied.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the disposition.")),
                            ("CaptureDate", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether selecting this disposition prompts the agent to capture a follow-up date.")),
                            ("Outcome", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "NotInService", "NoAnswer", "Busy", "AnsweringMachine").Description("The outcome the platform applies this disposition for when a call ends in a way nobody chose: NotInService (the dialer or an automated call found the number dead; recording it also excludes the number from future dialing), NoAnswer, Busy or AnsweringMachine (an automated call).")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("disposition")))
                        .AdditionalProperties(true))
                    .Description("Dispositions to create or update.")))
            .Required("name", "Dispositions")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
