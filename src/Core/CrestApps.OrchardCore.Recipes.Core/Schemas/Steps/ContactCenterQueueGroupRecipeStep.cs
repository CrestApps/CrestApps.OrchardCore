using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterQueueGroup" recipe step — imports Contact Center queue groups used to organize queues for administration and reporting.
/// </summary>
public sealed class ContactCenterQueueGroupRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterQueueGroup";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterQueueGroup").Description("Recipe step discriminator. Must be 'ContactCenterQueueGroup'.")),
                ("QueueGroups", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("queue group")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique queue-group name.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Queue-group description.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("queue group")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The Contact Center queue groups to create or update.")))
            .Required("name", "QueueGroups")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
