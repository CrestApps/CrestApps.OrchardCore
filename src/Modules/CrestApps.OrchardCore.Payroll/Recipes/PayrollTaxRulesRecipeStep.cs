using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports payroll tax rules, matched by id and then by tax code and first day (a dated version).
/// </summary>
internal sealed class PayrollTaxRulesRecipeStep : CatalogRecipeStep<PayrollTaxRule>
{
    public const string EntriesName = "Rules";

    private readonly IPayrollTaxRuleManager _manager;

    public PayrollTaxRulesRecipeStep(IPayrollTaxRuleManager manager)
        : base(PayrollConstants.TaxRulesRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<PayrollTaxRule> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var code = data["TaxCode"]?.GetValue<string>()?.Trim().ToUpperInvariant();
        var from = data["EffectiveFrom"]?.GetValue<string>();

        return string.IsNullOrEmpty(code)
            ? null
            : (await _manager.ListAsync()).FirstOrDefault(rule => rule.TaxCode == code && rule.EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) == from);
    }
}

/// <summary>
/// Describes the "PayrollTaxRules" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayrollTaxRulesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.TaxRulesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.TaxRulesRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollTaxRules'.")),
                (PayrollTaxRulesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The rule's 26-character id; omit it to match by tax code and first day, or create one.")),
                            ("Name", Text("What a statement shows.")),
                            ("TaxCode", Text("The tax's code, the same for every dated version, such as US-CA-SIT.")),
                            ("Jurisdiction", Text("The jurisdiction, such as US or US-CA.")),
                            ("Category", Text("The tax category whose wages are taxed: IncomeTax, SocialInsurance, Unemployment or one a country pack adds.")),
                            ("Payer", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employee", "Employer").Description("Who pays.")),
                            ("Method", Text("A Taxation calculation method: Percentage, FixedAmount, TaxTable, Progressive or Threshold.")),
                            ("Rate", Number("The rate as a percent, such as 6.2.")),
                            ("FixedAmount", Number("The fixed amount.")),
                            ("TaxTableId", Text("The id of a Taxation tax table (rates in its rows are fractions).")),
                            ("WageBase", Number("The most wages taxed in a calendar year.")),
                            ("Annualize", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Work the tax out on a year's wages.")),
                            ("UsesElections", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Apply the person's election: exempt, allowances, extra withholding.")),
                            ("AllowanceAmount", Number("What one allowance lowers a year's wages by.")),
                            ("SupplementalRate", Number("The flat rate, as a percent, of supplemental wages.")),
                            ("EffectiveFrom", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The first pay date the rule applies to.")),
                            ("EffectiveTo", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("The last pay date, or null.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule is in use.")))
                        .Required("TaxCode", "Jurisdiction", "EffectiveFrom")
                        .AdditionalProperties(true))
                    .Description("The tax rules to create, or update by id or by tax code and first day.")))
            .Required("name", PayrollTaxRulesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);
}
