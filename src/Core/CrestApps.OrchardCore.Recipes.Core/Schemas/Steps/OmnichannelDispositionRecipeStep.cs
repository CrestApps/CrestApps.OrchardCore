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
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Stable identifier of the disposition. When it matches an existing disposition the entry is updated; otherwise a new disposition is created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Unique name of the disposition.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Legacy display text. Prefer 'Name'; this is only used to seed the name when 'Name' is not supplied.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the disposition.")),
                            ("CaptureDate", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether selecting this disposition prompts the agent to capture a follow-up date.")))
                        .AdditionalProperties(true))
                    .Description("Dispositions to create or update.")))
            .Required("name", "Dispositions")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
