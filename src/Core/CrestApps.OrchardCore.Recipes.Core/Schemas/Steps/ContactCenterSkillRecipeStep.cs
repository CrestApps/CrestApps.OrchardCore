using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterSkill" recipe step — imports Contact Center skills that can be assigned to agents and required by queues.
/// </summary>
public sealed class ContactCenterSkillRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterSkill";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterSkill").Description("Recipe step discriminator. Must be 'ContactCenterSkill'.")),
                ("Skills", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing skill is updated instead of a new one being created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique skill name.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Skill description.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the skill can be selected by agents and queues.")))
                        .AdditionalProperties(true))
                    .Description("The Contact Center skills to create or update.")))
            .Required("name", "Skills")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
