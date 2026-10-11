using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Schema for the "TaxType" recipe step — creates or updates tax types.
/// </summary>
public sealed class TaxTypeRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => TaxationDeploymentSteps.TaxType;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var itemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The stable identifier of the tax type. Preserved across environments.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the tax type (for example 'SalesTax' or 'VAT'). Stored on tax lines.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("An optional description of the tax type.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationDeploymentSteps.TaxType).Description($"Recipe step discriminator. Must be '{TaxationDeploymentSteps.TaxType}'.")),
                ("TaxTypes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(1)
                    .Description("The tax types to create or update.")))
            .Required("name", "TaxTypes")
            .AdditionalProperties(true)
            .Build();
    }
}
