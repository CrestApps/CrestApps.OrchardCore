using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Schema for the "TaxTable" recipe step — creates or updates tax tables.
/// </summary>
public sealed class TaxTableRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => TaxationDeploymentSteps.TaxTable;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var dateSchema = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.String),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));

        var nullableNumber = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.Number),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));

        var rowSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Minimum", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The inclusive lower bound the row applies to.")),
                ("Maximum", nullableNumber.Description("The exclusive upper bound the row applies to. Omit for no upper bound.")),
                ("Rate", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The rate applied within the row, expressed as a fraction (for example 0.2 for 20%).")),
                ("FixedAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("A fixed amount applied within the row.")),
                ("BaseAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("A base amount added before the rate is applied, used by tiered schedules.")))
            .Required("Minimum")
            .AdditionalProperties(true);

        var itemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The stable identifier of the tax table. Preserved across environments.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the tax table, referenced by table-based tax rules.")),
                ("Version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The version of the tax table.")),
                ("EffectiveFromUtc", dateSchema.Description("The UTC date the tax table becomes effective.")),
                ("EffectiveToUtc", dateSchema.Description("The UTC date the tax table stops being effective.")),
                ("Rows", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(rowSchema)
                    .Description("The rows that make up the tax table.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationDeploymentSteps.TaxTable).Description($"Recipe step discriminator. Must be '{TaxationDeploymentSteps.TaxTable}'.")),
                ("TaxTables", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(1)
                    .Description("The tax tables to create or update.")))
            .Required("name", "TaxTables")
            .AdditionalProperties(true)
            .Build();
    }
}
