using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Schema for the "TaxJurisdiction" recipe step — creates or updates tax jurisdictions.
/// </summary>
public sealed class TaxJurisdictionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => TaxationDeploymentSteps.TaxJurisdiction;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var levelSchema = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Enum("Country", "State", "Province", "Region", "County", "City", "Special", "Other"),
            new JsonSchemaBuilder().Type(SchemaValueType.Integer))
            .Description("The administrative level of the jurisdiction.");

        var dateSchema = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.String),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));

        var itemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The stable identifier of the jurisdiction. Preserved across environments.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The display name of the jurisdiction.")),
                ("Code", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The code of the jurisdiction.")),
                ("Level", levelSchema),
                ("Country", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The ISO country code the jurisdiction belongs to.")),
                ("Region", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The state, province, or region code the jurisdiction belongs to.")),
                ("County", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The county the jurisdiction belongs to.")),
                ("City", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The city the jurisdiction belongs to.")),
                ("PostalCode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The postal code the jurisdiction covers.")),
                ("ParentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The identifier of the parent jurisdiction, forming a hierarchy.")),
                ("EffectiveFromUtc", dateSchema.Description("The UTC date the jurisdiction becomes effective.")),
                ("EffectiveToUtc", dateSchema.Description("The UTC date the jurisdiction stops being effective.")))
            .Required("Name", "Code")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationDeploymentSteps.TaxJurisdiction).Description($"Recipe step discriminator. Must be '{TaxationDeploymentSteps.TaxJurisdiction}'.")),
                ("TaxJurisdictions", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(1)
                    .Description("The tax jurisdictions to create or update.")))
            .Required("name", "TaxJurisdictions")
            .AdditionalProperties(true)
            .Build();
    }
}
