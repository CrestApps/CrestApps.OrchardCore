using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Imports income tax rules, matched by id and then by tax code and first day (a dated version).
/// </summary>
internal sealed class IncomeTaxRulesRecipeStep : CatalogRecipeStep<IncomeTaxRule>
{
    public const string EntriesName = "Rules";

    private readonly IIncomeTaxRuleManager _manager;

    public IncomeTaxRulesRecipeStep(IIncomeTaxRuleManager manager)
        : base(TaxationConstants.IncomeTaxRulesRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<IncomeTaxRule> FindExistingAsync(string id, JsonObject data)
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
/// Describes the "IncomeTaxRules" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class IncomeTaxRulesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TaxationConstants.IncomeTaxRulesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationConstants.IncomeTaxRulesRecipeStepName).Description("Recipe step discriminator. Must be 'IncomeTaxRules'.")),
                (IncomeTaxRulesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The rule's 26-character id; omit it to match by tax code and first day, or create one.")),
                            ("Name", Text("What a statement shows.")),
                            ("TaxCode", Text("The tax's code, the same for every dated version, such as US-CA-SIT or CA-CPP.")),
                            ("Jurisdiction", Text("The jurisdiction, such as US, US-CA, CA or CA-QC.")),
                            ("CollectedBy", Text("The jurisdiction whose registration covers the tax when it isn't the rule's own, such as CA for a province's tax the CRA collects.")),
                            ("ExceptJurisdictions", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Jurisdictions whose people the rule doesn't apply to, such as CA-QC for the Canada Pension Plan.")),
                            ("ByPlaceOfWork", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Match the jurisdiction by where the person works when that is known, never by where they live (Canada's province of employment).")),
                            ("IncomeKinds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employment", "Contractor", "Pension", "Other")).Description("The kinds of income the rule applies to; empty for employment income (or contractor payments for ContractorWithholding).")),
                            ("Category", Text("The tax category whose income is taxed: IncomeTax, SocialInsurance, Unemployment or one a country pack adds.")),
                            ("Payer", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employee", "Employer").Description("Who pays.")),
                            ("Source", Text("The kind of rule, which decides the fields its editor shows: FlatRate, ShareOfTax, TaxTable, FixedAmount, or one a country pack adds, such as UnitedStatesFormW4, CanadaFederal, CanadaProvincial or QuebecTP1015. Omitted, it comes from the method and the share.")),
                            ("Method", Text("A Taxation calculation method (Percentage, FixedAmount, TaxTable, Progressive or Threshold) or a withholding method a country pack adds, such as UnitedStatesFormW4, CanadaFederal, CanadaProvincial or QuebecTP1015.")),
                            ("Rate", Number("The rate as a percent, such as 6.2.")),
                            ("FixedAmount", Number("The fixed amount.")),
                            ("TaxTableId", Text("The id of a Taxation tax table (rates in its rows are fractions).")),
                            ("WageBase", Number("The most income taxed in a calendar year.")),
                            ("WageThreshold", Number("The income of a calendar year above which the tax starts; only the part over it is taxed (such as CPP2 above the first ceiling).")),
                            ("YearlyExemption", Number("An amount of a year's income never taxed, taken off each pay in proportion to the periods of a year, such as the CPP basic exemption.")),
                            ("YearlyMaximum", Number("The most tax taken in a calendar year, such as a contribution's maximum.")),
                            ("ShareOfTaxCode", Text("The code of another tax of the same pay this rule is a share of, such as the employer's share of a contribution.")),
                            ("ShareMultiplier", Number("How many times that tax the rule takes, such as 1.4 for the employer's EI premium.")),
                            ("Tables", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("A withholding method's Taxation tax table ids, by the method's key (for UnitedStatesFormW4: Single, MarriedFilingJointly, HeadOfHousehold and each with .Step2).")),
                            ("Parameters", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.Number)).Description("A withholding method's amounts, by the method's key (for UnitedStatesFormW4: AdjustmentMarriedFilingJointly and Adjustment).")),
                            ("Annualize", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Work the tax out on a year's wages.")),
                            ("UsesElections", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Apply the person's election: exempt, allowances, extra withholding.")),
                            ("AllowanceAmount", Number("What one allowance lowers a year's wages by.")),
                            ("SupplementalRate", Number("The flat rate, as a percent, of supplemental wages.")),
                            ("SupplementalExcessRate", Number("The rate, as a percent, of a year's supplemental wages above SupplementalExcessThreshold, whatever the method.")),
                            ("SupplementalExcessThreshold", Number("The supplemental wages of a calendar year above which SupplementalExcessRate applies.")),
                            ("EffectiveFrom", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The first pay date the rule applies to.")),
                            ("EffectiveTo", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("The last pay date, or null.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule is in use.")))
                        .Required("TaxCode", "Jurisdiction", "EffectiveFrom")
                        .AdditionalProperties(true))
                    .Description("The tax rules to create, or update by id or by tax code and first day.")))
            .Required("name", IncomeTaxRulesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);
}
