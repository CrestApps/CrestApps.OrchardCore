using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "OmnichannelLeadStatus" recipe step — imports the lead statuses of the CRM.
/// </summary>
public sealed class OmnichannelLeadStatusRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "OmnichannelLeadStatus";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("OmnichannelLeadStatus").Description("Recipe step discriminator. Must be 'OmnichannelLeadStatus'.")),
                ("LeadStatuses", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("lead status")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Unique name of the lead status.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of the lead status.")),
                            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Position of the status in lists, lowest first.")),
                            ("IsDefault", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a new lead starts in this status.")),
                            ("IsClosed", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a lead in this status is closed.")),
                            ("IsConverted", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether this is the status a lead takes when it is converted.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("lead status")))
                        .AdditionalProperties(true))
                    .Description("The lead statuses to create or update.")))
            .Required("name", "LeadStatuses")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
