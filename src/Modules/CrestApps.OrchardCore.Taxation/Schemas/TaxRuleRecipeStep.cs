using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Schemas;

/// <summary>
/// Schema for the "TaxRule" recipe step — creates or updates tax rules.
/// </summary>
public sealed class TaxRuleRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => TaxationDeploymentSteps.TaxRule;

    /// <inheritdoc />
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var customerTypeSchema = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("B2C", "B2B"),
            new JsonSchemaBuilder().Type(SchemaValueType.Integer),
            new JsonSchemaBuilder().Type(SchemaValueType.Null))
            .Description("The customer classification the rule applies to. Omit to match every customer type.");

        var number = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.Number),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));

        var dateSchema = new JsonSchemaBuilder().AnyOf(
            new JsonSchemaBuilder().Type(SchemaValueType.String),
            new JsonSchemaBuilder().Type(SchemaValueType.Null));

        var itemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The stable identifier of the rule. Preserved across environments.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The display name of the rule.")),
                ("Version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The version of the rule.")),
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule is enabled.")),
                ("Priority", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The evaluation priority. Lower values are evaluated first.")),
                ("TaxType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The type of tax the rule produces (for example 'SalesTax').")),
                ("TaxName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The human readable name of the tax the rule produces.")),
                ("TaxCode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The code of the tax the rule produces.")),
                ("JurisdictionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The identifier of the jurisdiction the rule belongs to.")),
                ("CategoryCode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The tax category code the rule applies to. Omit to match every category.")),
                ("CustomerType", customerTypeSchema),
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The calculation method used by the rule (for example 'Percentage').")),
                ("Rate", number.Description("The rate applied by the rule, expressed as a fraction (for example 0.2 for 20%).")),
                ("FixedAmount", number.Description("The fixed amount applied by the rule, when the method is amount based.")),
                ("TaxTableId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The identifier of the tax table the rule uses, when the method is table based.")),
                ("IncludedInPrice", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the produced tax is included in the item price.")),
                ("IsCompound", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the produced tax is compound (calculated on top of other taxes).")),
                ("ReverseCharge", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the recipient accounts for the tax (reverse charge). Produces a zero-amount reverse-charge line when the customer matches.")),
                ("AppliesToShipping", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule applies to shipping charges.")),
                ("MinimumAmount", number.Description("The inclusive minimum taxable amount the rule applies to.")),
                ("MaximumAmount", number.Description("The exclusive maximum taxable amount the rule applies to.")),
                ("EffectiveFromUtc", dateSchema.Description("The UTC date the rule becomes effective.")),
                ("EffectiveToUtc", dateSchema.Description("The UTC date the rule stops being effective.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationDeploymentSteps.TaxRule).Description($"Recipe step discriminator. Must be '{TaxationDeploymentSteps.TaxRule}'.")),
                ("TaxRules", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(1)
                    .Description("The tax rules to create or update.")))
            .Required("name", "TaxRules")
            .AdditionalProperties(true)
            .Build();
    }
}
