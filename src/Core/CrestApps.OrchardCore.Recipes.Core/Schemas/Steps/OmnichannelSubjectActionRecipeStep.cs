using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelSubjectAction" recipe step — imports the actions a subject disposition triggers.
/// </summary>
public sealed class OmnichannelSubjectActionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelSubjectAction";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelSubjectAction").Description("Recipe step discriminator. Must be 'OmnichannelSubjectAction'.")),
                ("SubjectActions", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Stable identifier of the subject action. When it matches an existing action the entry is updated; otherwise a new action is created.")),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Source that owns the subject action. A subject action cannot be imported without a source.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Human-readable name of the subject action.")),
                            ("SubjectContentType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Subject content type this action belongs to.")),
                            ("DispositionId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the disposition that triggers this action.")),
                            ("SetDoNotCall", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether to set the contact's 'Do Not Call' preference when this action runs.")),
                            ("SetDoNotSms", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether to set the contact's 'Do Not SMS' preference when this action runs.")),
                            ("SetDoNotEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether to set the contact's 'Do Not Email' preference when this action runs.")),
                            ("SetDoNotChat", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether to set the contact's 'Do Not Chat' preference when this action runs.")))
                        .Required("Source")
                        .AdditionalProperties(true))
                    .Description("Subject actions to create or update.")))
            .Required("name", "SubjectActions")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
