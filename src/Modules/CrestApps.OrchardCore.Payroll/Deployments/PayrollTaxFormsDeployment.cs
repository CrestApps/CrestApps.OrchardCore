using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Deployments;

/// <summary>
/// Exports the tax forms: who each reports, its fields and formulas, yearly values, checks and statement texts.
/// </summary>
public sealed class PayrollTaxFormsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayrollTaxFormsDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayrollTaxFormsDeploymentStep>("Tax forms");

    /// <inheritdoc />
    public override string Name => PayrollConstants.TaxFormsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class PayrollTaxFormsDeploymentSource : CatalogDeploymentSource<PayrollTaxFormsDeploymentStep, PayrollTaxForm>
{
    public PayrollTaxFormsDeploymentSource(IPayrollTaxFormManager manager)
        : base(PayrollTaxFormsRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<PayrollTaxForm> Order(IEnumerable<PayrollTaxForm> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.Ordinal);
}

/// <summary>
/// Imports tax forms, matched by id and then by name: a form that exists is replaced with the recipe's, fields included.
/// </summary>
internal sealed class PayrollTaxFormsRecipeStep : CatalogRecipeStep<PayrollTaxForm>
{
    public const string EntriesName = "Forms";

    private readonly IPayrollTaxFormManager _manager;

    public PayrollTaxFormsRecipeStep(IPayrollTaxFormManager manager)
        : base(PayrollConstants.TaxFormsRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<PayrollTaxForm> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var name = data["Name"]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(name) ? null : await _manager.FindByNameAsync(name);
    }
}

internal sealed class PayrollTaxFormsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayrollTaxFormsDeploymentStep);

    protected override string DisplayText => "Tax forms";

    protected override string Description => "Exports every tax form, with its fields, formulas, yearly values, checks and statement texts.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

/// <summary>
/// Describes the "PayrollTaxForms" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayrollTaxFormsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.TaxFormsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.TaxFormsRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollTaxForms'.")),
                (PayrollTaxFormsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The form's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The stored name filings keep, such as US-941: no spaces or colons.")),
                            ("DisplayName", Text("What people see, such as Form 941 (quarterly federal return).")),
                            ("Description", Text("What the form is for.")),
                            ("Jurisdiction", Text("The jurisdiction an employer must be registered in to file it, such as US or CA-QC.")),
                            ("Period", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Year", "Quarter").Description("The period a filing covers.")),
                            ("Workers", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employees", "Contractors", "Everyone").Description("Whose pay the form reports.")),
                            ("PeopleJurisdiction", Text("Only people with a tax line of this jurisdiction, or one under it, are reported; empty reports everyone.")),
                            ("HasPersonStatements", Boolean("Whether each person gets a statement (a W-2, a T4 slip), kept, printed and sent to them once filed.")),
                            ("StatementPerJurisdiction", Boolean("Whether a person gets a statement for each jurisdiction under the form's they paid tax in, such as a T4 slip per province.")),
                            ("ListJurisdictionAmounts", Boolean("Whether each person's statement lists the wages and income tax of every jurisdiction below the country, such as a W-2's boxes 15 to 20.")),
                            ("PersonCondition", Text("A formula over the person fields that says whether a person is reported; empty reports a person with any field not zero.")),
                            ("MissingTaxIdNote", Text("The note added for a reported person with no tax id on file, {0} being their name; empty adds none.")),
                            ("PayCodeFieldPrefix", Text("The prefix of the names pay codes report to a field by, such as W2:; empty uses the form's name and a colon.")),
                            ("Enabled", Boolean("Whether employers can prepare the form.")),
                            ("Fields", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Number", Text("The box or line number, such as 5a or 12D; formulas read it as [5a].")),
                                        ("Label", Text("The label, such as 5a Taxable social security wages.")),
                                        ("Level", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Person", "Filing").Description("On each person's statement, or on the filing.")),
                                        ("Formula", Text("The formula, such as GREATEST(0, TAXABLE('US-SS-EE') - [5b]); empty is zero plus what pay codes report.")),
                                        ("AddToFiling", Boolean("Whether a person field is also added up on the filing, under the same number.")),
                                        ("PayCodes", Boolean("Whether pay codes can report their amounts to the field.")),
                                        ("Print", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Never", "Always", "WhenNotZero").Description("When a person field is printed on the person's statement.")))
                                    .Required("Number", "Level")
                                    .AdditionalProperties(false))
                                .Description("The fields (the form's boxes and lines), in the order they are shown.")),
                            ("Values", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Name", Text("The name formulas read it by with VALUE('Name').")),
                                        ("Year", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The first year it applies to.")),
                                        ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The value.")))
                                    .Required("Name", "Year", "Amount")
                                    .AdditionalProperties(false))
                                .Description("Yearly values: the latest year at or before the filing's applies.")),
                            ("Checks", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Condition", Text("A formula over the filing fields that must be true.")),
                                        ("Note", Text("The note added to the filing when it is not.")))
                                    .Required("Condition", "Note")
                                    .AdditionalProperties(false))
                                .Description("Checks that add a note to a filing when they fail.")),
                            ("Statement", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Name", Text("What a person's statement is called, such as Form W-2.")),
                                    ("Title", Text("The printed title.")),
                                    ("Copy", Text("The line under the title.")),
                                    ("TaxIdLabel", Text("The label of the person's tax id.")),
                                    ("TaxIdFormat", Text("How the masked tax id is printed, {0} being its last characters.")),
                                    ("EmployerTaxIdLabel", Text("The label of the employer's number.")),
                                    ("EmployerAccountJurisdiction", Text("The jurisdiction of the employer registration whose account number is printed; empty prints the employer's tax id.")),
                                    ("FileName", Text("The start of the PDF's file name, such as W-2.")))
                                .AdditionalProperties(false)
                                .Description("How a person's statement is printed.")))
                        .Required("Name", "DisplayName", "Jurisdiction", "Fields")
                        .AdditionalProperties(true))
                    .Description("The tax forms to create, or replace by id or name.")))
            .Required("name", PayrollTaxFormsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);
}
